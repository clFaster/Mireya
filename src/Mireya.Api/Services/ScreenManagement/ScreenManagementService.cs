using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Mireya.Api.Constants;
using Mireya.Database;
using Mireya.Database.Models;
using NanoidDotNet;

namespace Mireya.Api.Services.ScreenManagement;

public interface IScreenManagementService
{
    /// <summary>
    /// Registers a new screen and generates a unique token to identify it
    /// </summary>
    Task<RegisterScreenResponse> RegisterScreenAsync(RegisterScreenRequest request);
    
    /// <summary>
    /// Gets screen details for the authenticated user (Bonjour call)
    /// </summary>
    Task<BonjourResponse> GetBonjourAsync(string userId);
    
    /// <summary>
    /// Gets a paginated list of screens with optional filtering
    /// </summary>
    Task<PagedScreensResponse> GetScreensAsync(int page, int pageSize, ApprovalStatus? status, string? sortBy);
    
    /// <summary>
    /// Gets details of a specific screen by ID
    /// </summary>
    Task<ScreenDetailsResponse> GetScreenByIdAsync(Guid id);
    
    /// <summary>
    /// Updates screen details (name, location, description)
    /// </summary>
    Task<ScreenDetailsResponse> UpdateScreenAsync(Guid id, UpdateScreenRequest request);
    
    /// <summary>
    /// Approves a screen and creates a user account for it
    /// </summary>
    Task<ApproveScreenResponse> ApproveScreenAsync(Guid id);
    
    /// <summary>
    /// Rejects a screen registration
    /// </summary>
    Task<ScreenDetailsResponse> RejectScreenAsync(Guid id);
}

public class ScreenManagementService(
    MireyaDbContext db,
    UserManager<User> userManager,
    ILogger<ScreenManagementService> logger,
    IScreenSynchronizationService syncService) : IScreenManagementService
{
    public async Task<RegisterScreenResponse> RegisterScreenAsync(RegisterScreenRequest request)
    {
        // Create a user account for the screen immediately
        var screenUser = new User
        {
            UserName = request.Username,
            Email = $"{request.Username}@mireya.local",
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow
        };
        
        var result = await userManager.CreateAsync(screenUser, request.Password);
        
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            logger.LogError("Failed to create user for screen registration: {Errors}", errors);
            throw new InvalidOperationException($"Failed to create user account: {errors}");
        }
        
        // Add the Screen role
        await userManager.AddToRoleAsync(screenUser, Roles.Screen);
        
        // Generate unique screen identifier
        var screenIdentifier = await Nanoid.GenerateAsync(
            size: NanoIdGen.ScreenIdentifierLength, 
            alphabet: NanoIdGen.HexAlphabet);
        while (await db.Displays.AnyAsync(d => d.ScreenIdentifier == screenIdentifier))
        {
            screenIdentifier = await Nanoid.GenerateAsync(
                size: NanoIdGen.ScreenIdentifierLength, 
                alphabet: NanoIdGen.HexAlphabet);
        }
        
        var display = new Display
        {
            Name = string.IsNullOrEmpty(request.DeviceName) ? $"Screen {await db.Displays.CountAsync() + 1}" : request.DeviceName,
            ScreenIdentifier = screenIdentifier,
            UserId = screenUser.Id,
            ResolutionWidth = request.ResolutionWidth,
            ResolutionHeight = request.ResolutionHeight,
            LastSeenAt = DateTime.UtcNow,
            ApprovalStatus = ApprovalStatus.Pending
        };
        
        db.Displays.Add(display);
        await db.SaveChangesAsync();
        
        logger.LogInformation("New screen registered with ID {DisplayId} and User {UserId}", display.Id, screenUser.Id);
        
        return new RegisterScreenResponse
        {
            ScreenIdentifier = screenIdentifier,
            UserId = screenUser.Id,
            ScreenName = display.Name
        };
    }

    public async Task<BonjourResponse> GetBonjourAsync(string userId)
    {
        var display = await db.Displays.FirstOrDefaultAsync(d => d.UserId == userId);
        
        if (display == null)
        {
            throw new KeyNotFoundException($"No screen found for user {userId}");
        }
        
        // Update last seen timestamp
        display.LastSeenAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        
        logger.LogInformation("Screen {DisplayId} called bonjour (User: {UserId})", display.Id, userId);
        
        return new BonjourResponse
        {
            ScreenIdentifier = display.ScreenIdentifier,
            ScreenName = display.Name,
            Description = display.Description,
            ApprovalStatus = display.ApprovalStatus.ToString(),
            Location = display.Location
        };
    }

    public async Task<PagedScreensResponse> GetScreensAsync(
        int page, 
        int pageSize, 
        ApprovalStatus? status, 
        string? sortBy)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 100) pageSize = 10;
        
        var query = db.Displays.AsQueryable();
        
        // Filter by status if provided
        if (status.HasValue)
        {
            query = query.Where(d => d.ApprovalStatus == status.Value);
        }
        
        // Apply sorting
        query = sortBy?.ToLower() switch
        {
            "name" => query.OrderBy(d => d.Name),
            "location" => query.OrderBy(d => d.Location),
            "status" => query.OrderBy(d => d.ApprovalStatus).ThenBy(d => d.Name),
            "lastseen" => query.OrderByDescending(d => d.LastSeenAt),
            _ => query.OrderByDescending(d => d.CreatedAt) // Default: newest first
        };
        
        var total = await query.CountAsync();
        var displays = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        
        var items = displays.Select(MapToDetailsResponse).ToList();
        
        return new PagedScreensResponse
        {
            Total = total,
            Page = page,
            PageSize = pageSize,
            Items = items
        };
    }

    public async Task<ScreenDetailsResponse> GetScreenByIdAsync(Guid id)
    {
        var display = await db.Displays.FindAsync(id);
        
        return display == null ? 
            throw new KeyNotFoundException($"Screen with ID {id} not found") : 
            MapToDetailsResponse(display);
    }

    public async Task<ScreenDetailsResponse> UpdateScreenAsync(Guid id, UpdateScreenRequest request)
    {
        var display = await db.Displays.FindAsync(id);
        
        if (display == null)
        {
            throw new KeyNotFoundException($"Screen with ID {id} not found");
        }
        
        // Update only provided fields
        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            display.Name = request.Name;
        }
        
        if (!string.IsNullOrWhiteSpace(request.Description))
        {
            display.Description = request.Description;
        }
        
        if (!string.IsNullOrWhiteSpace(request.Location))
        {
            display.Location = request.Location;
        }
        
        display.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        
        logger.LogInformation("Screen {DisplayId} updated", display.Id);
        
        // Notify screen of updates
        await syncService.SyncScreenAsync(display.Id);

        return MapToDetailsResponse(display);
    }

    public async Task<ApproveScreenResponse> ApproveScreenAsync(Guid id)
    {
        var display = await db.Displays.FindAsync(id);
        
        if (display == null)
        {
            throw new KeyNotFoundException($"Screen with ID {id} not found");
        }
        
        if (display.ApprovalStatus == ApprovalStatus.Approved)
        {
            logger.LogInformation("Screen {DisplayId} is already approved", display.Id);
            return new ApproveScreenResponse { Screen = MapToDetailsResponse(display) };
        }
        
        if (string.IsNullOrEmpty(display.UserId))
        {
            throw new InvalidOperationException($"Screen {id} has no associated user account. It may need to be re-registered.");
        }
        
        // Simply update the approval status
        display.ApprovalStatus = ApprovalStatus.Approved;
        display.UpdatedAt = DateTime.UtcNow;
        
        await db.SaveChangesAsync();
        
        logger.LogInformation("Screen {DisplayId} approved (User: {UserId})", display.Id, display.UserId);

        // Notify screen of approval
        await syncService.SyncScreenAsync(display.Id);

        return new ApproveScreenResponse { Screen = MapToDetailsResponse(display) };
    }

    public async Task<ScreenDetailsResponse> RejectScreenAsync(Guid id)
    {
        var display = await db.Displays.FindAsync(id);
        
        if (display == null)
        {
            throw new KeyNotFoundException($"Screen with ID {id} not found");
        }
        
        display.ApprovalStatus = ApprovalStatus.Rejected;
        display.UpdatedAt = DateTime.UtcNow;
        
        await db.SaveChangesAsync();
        
        logger.LogInformation("Screen {DisplayId} rejected", display.Id);
        
        return MapToDetailsResponse(display);
    }
    
    private static ScreenDetailsResponse MapToDetailsResponse(Display display)
    {
        return new ScreenDetailsResponse
        {
            Id = display.Id,
            Name = display.Name,
            Description = display.Description,
            Location = display.Location,
            ScreenIdentifier = display.ScreenIdentifier,
            ApprovalStatus = display.ApprovalStatus.ToString(),
            UserId = display.UserId,
            ResolutionWidth = display.ResolutionWidth,
            ResolutionHeight = display.ResolutionHeight,
            IsActive = display.IsActive,
            LastSeenAt = display.LastSeenAt,
            CreatedAt = display.CreatedAt,
            UpdatedAt = display.UpdatedAt
        };
    }
}
