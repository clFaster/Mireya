using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Mireya.Application.Constants;
using Mireya.Application.Services.Audit;
using Mireya.Application.Services.Campaign;
using Mireya.Database;
using Mireya.Database.Models;
using NanoidDotNet;

namespace Mireya.Application.Services.ScreenManagement;

public interface IScreenManagementService
{
    /// <summary>
    ///     Registers a new screen and generates a unique token to identify it
    /// </summary>
    Task<RegisterScreenResponse> RegisterScreenAsync(RegisterScreenRequest request);

    /// <summary>
    ///     Gets screen details for the authenticated user (Bonjour call)
    /// </summary>
    Task<BonjourResponse> GetBonjourAsync(string userId);

    /// <summary>
    ///     Gets a paginated list of screens with optional filtering
    /// </summary>
    Task<PagedScreensResponse> GetScreensAsync(
        int page,
        int pageSize,
        ApprovalStatus? status,
        string? sortBy
    );

    /// <summary>
    ///     Gets details of a specific screen by ID
    /// </summary>
    Task<ScreenDetailsResponse> GetScreenByIdAsync(Guid id);

    /// <summary>
    ///     Updates screen details (name, location, description)
    /// </summary>
    Task<ScreenDetailsResponse> UpdateScreenAsync(Guid id, UpdateScreenRequest request);

    /// <summary>
    ///     Approves a screen and creates a user account for it
    /// </summary>
    Task<ApproveScreenResponse> ApproveScreenAsync(Guid id);

    /// <summary>
    ///     Rejects a screen registration
    /// </summary>
    Task<ScreenDetailsResponse> RejectScreenAsync(Guid id);

    /// <summary>
    ///     Marks a screen as active/online and updates LastSeenAt
    /// </summary>
    Task SetScreenActiveAsync(string userId, bool isActive);

    /// <summary>
    ///     Gets the count of screens with a specific approval status
    /// </summary>
    Task<int> GetScreenCountByStatusAsync(ApprovalStatus status);

    /// <summary>
    ///     Gets screen details with assigned campaigns
    /// </summary>
    Task<ScreenWithCampaignsResponse> GetScreenWithCampaignsAsync(Guid id);

    /// <summary>
    ///     Updates screen details and campaign assignments
    /// </summary>
    Task UpdateScreenWithCampaignsAsync(
        Guid id,
        UpdateScreenRequest request,
        List<CampaignAssignmentRequest> assignments
    );

    Task UpdateScreenCampaignAssignmentsAsync(Guid id, List<CampaignAssignmentRequest> assignments);

    /// <summary>
    ///     Gets all approved screens, optionally filtering by active status
    /// </summary>
    Task<List<ScreenDetailsResponse>> GetApprovedScreensAsync(bool includeOffline);

    /// <summary>
    ///     Sends a remote command (e.g. restart playback, reload content) to a connected screen.
    ///     Returns false if the screen is unknown or not currently reachable.
    /// </summary>
    Task<bool> SendCommandAsync(Guid id, string command);
}

public class ScreenManagementService(
    MireyaDbContext db,
    UserManager<User> userManager,
    ILogger<ScreenManagementService> logger,
    IScreenSynchronizationService syncService,
    IAuditService audit
) : IScreenManagementService
{
    private const string ScreenAuditEntity = "Screen";

    public async Task<RegisterScreenResponse> RegisterScreenAsync(RegisterScreenRequest request)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();

        // Create a user account for the screen immediately
        var screenUser = new User
        {
            UserName = request.Username,
            Email = $"{request.Username}@mireya.local",
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow,
        };

        var result = await userManager.CreateAsync(screenUser, request.Password);

        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            logger.LogError("Failed to create user for screen registration: {Errors}", errors);
            throw new InvalidOperationException($"Failed to create user account: {errors}");
        }

        // Add the Screen role
        var roleResult = await userManager.AddToRoleAsync(screenUser, Roles.Screen);
        if (!roleResult.Succeeded)
        {
            var errors = string.Join(", ", roleResult.Errors.Select(e => e.Description));
            logger.LogError("Failed to assign the screen role: {Errors}", errors);
            throw new InvalidOperationException($"Failed to assign screen role: {errors}");
        }

        // Generate unique screen identifier
        var screenIdentifier = await Nanoid.GenerateAsync(
            size: NanoIdGen.ScreenIdentifierLength,
            alphabet: NanoIdGen.HexAlphabet
        );
        while (await db.Screens.AnyAsync(d => d.ScreenIdentifier == screenIdentifier))
            screenIdentifier = await Nanoid.GenerateAsync(
                size: NanoIdGen.ScreenIdentifierLength,
                alphabet: NanoIdGen.HexAlphabet
            );

        var screen = new Screen
        {
            Name = string.IsNullOrEmpty(request.DeviceName)
                ? $"Screen {await db.Screens.CountAsync() + 1}"
                : request.DeviceName,
            ScreenIdentifier = screenIdentifier,
            UserId = screenUser.Id,
            ResolutionWidth = request.ResolutionWidth,
            ResolutionHeight = request.ResolutionHeight,
            LastSeenAt = DateTime.UtcNow,
            ApprovalStatus = ApprovalStatus.Pending,
        };

        db.Screens.Add(screen);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        logger.LogInformation(
            "New screen registered with ID {ScreenId} and User {UserId}",
            screen.Id,
            screenUser.Id
        );

        return new RegisterScreenResponse
        {
            ScreenIdentifier = screenIdentifier,
            UserId = screenUser.Id,
            ScreenName = screen.Name,
        };
    }

    public async Task<BonjourResponse> GetBonjourAsync(string userId)
    {
        var screen = await db.Screens.FirstOrDefaultAsync(d => d.UserId == userId);

        if (screen == null)
            throw new KeyNotFoundException($"No screen found for user {userId}");

        // Update last seen timestamp
        screen.LastSeenAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        logger.LogInformation(
            "Screen {ScreenId} called bonjour (User: {UserId})",
            screen.Id,
            userId
        );

        return new BonjourResponse
        {
            ScreenIdentifier = screen.ScreenIdentifier,
            ScreenName = screen.Name,
            Description = screen.Description,
            ApprovalStatus = screen.ApprovalStatus.ToString(),
            Location = screen.Location,
        };
    }

    public async Task<PagedScreensResponse> GetScreensAsync(
        int page,
        int pageSize,
        ApprovalStatus? status,
        string? sortBy
    )
    {
        if (page < 1)
            page = 1;
        if (pageSize is < 1 or > 100)
            pageSize = 10;

        var query = db.Screens.AsQueryable();

        if (status.HasValue)
            query = query.Where(d => d.ApprovalStatus == status.Value);

        query = ApplyScreenSorting(query, sortBy);

        var total = await query.CountAsync();
        var screens = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        var items = screens.Select(MapToDetailsResponse).ToList();

        return new PagedScreensResponse
        {
            Total = total,
            Page = page,
            PageSize = pageSize,
            Items = items,
        };
    }

    private static readonly Dictionary<
        string,
        Func<IQueryable<Screen>, IQueryable<Screen>>
    > SortFunctions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["name"] = q => q.OrderBy(d => d.Name),
        ["location"] = q => q.OrderBy(d => d.Location),
        ["status"] = q => q.OrderBy(d => d.ApprovalStatus).ThenBy(d => d.Name),
        ["lastseen"] = q => q.OrderByDescending(d => d.LastSeenAt),
    };

    private static IQueryable<Screen> ApplyScreenSorting(
        IQueryable<Screen> query,
        string? sortBy
    ) =>
        sortBy != null && SortFunctions.TryGetValue(sortBy, out var sort)
            ? sort(query)
            : query.OrderByDescending(d => d.CreatedAt);

    public async Task<ScreenDetailsResponse> GetScreenByIdAsync(Guid id)
    {
        var screen = await db.Screens.FindAsync(id);

        return screen == null
            ? throw new KeyNotFoundException($"Screen with ID {id} not found")
            : MapToDetailsResponse(screen);
    }

    public async Task<ScreenDetailsResponse> UpdateScreenAsync(Guid id, UpdateScreenRequest request)
    {
        var screen = await db.Screens.FindAsync(id);

        if (screen == null)
            throw new KeyNotFoundException($"Screen with ID {id} not found");

        // Update only provided fields
        if (!string.IsNullOrWhiteSpace(request.Name))
            screen.Name = request.Name;

        if (!string.IsNullOrWhiteSpace(request.Description))
            screen.Description = request.Description;

        if (!string.IsNullOrWhiteSpace(request.Location))
            screen.Location = request.Location;

        if (request.ShufflePlayback.HasValue)
            screen.ShufflePlayback = request.ShufflePlayback.Value;

        screen.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        logger.LogInformation("Screen {ScreenId} updated", screen.Id);

        // Notify screen of updates
        await syncService.SyncScreenAsync(screen.Id);

        return MapToDetailsResponse(screen);
    }

    public async Task<ApproveScreenResponse> ApproveScreenAsync(Guid id)
    {
        var screen = await db.Screens.FindAsync(id);

        if (screen == null)
            throw new KeyNotFoundException($"Screen with ID {id} not found");

        if (screen.ApprovalStatus == ApprovalStatus.Approved)
        {
            logger.LogInformation("Screen {ScreenId} is already approved", screen.Id);
            return new ApproveScreenResponse { Screen = MapToDetailsResponse(screen) };
        }

        if (string.IsNullOrEmpty(screen.UserId))
            throw new InvalidOperationException(
                $"Screen {id} has no associated user account. It may need to be re-registered."
            );

        // Simply update the approval status
        screen.ApprovalStatus = ApprovalStatus.Approved;
        screen.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        logger.LogInformation(
            "Screen {ScreenId} approved (User: {UserId})",
            screen.Id,
            screen.UserId
        );

        // Notify screen of approval
        await syncService.SyncScreenAsync(screen.Id);

        await audit.LogAsync(
            "Approved",
            ScreenAuditEntity,
            screen.Id.ToString(),
            $"Approved screen '{screen.Name}'"
        );

        return new ApproveScreenResponse { Screen = MapToDetailsResponse(screen) };
    }

    public async Task<ScreenDetailsResponse> RejectScreenAsync(Guid id)
    {
        var screen = await db.Screens.FindAsync(id);

        if (screen == null)
            throw new KeyNotFoundException($"Screen with ID {id} not found");

        screen.ApprovalStatus = ApprovalStatus.Rejected;
        screen.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        // Immediately revoke any active playlist on a connected screen.
        await syncService.SyncScreenAsync(screen.Id);

        logger.LogInformation("Screen {ScreenId} rejected", screen.Id);

        await audit.LogAsync(
            "Rejected",
            ScreenAuditEntity,
            screen.Id.ToString(),
            $"Rejected screen '{screen.Name}'"
        );

        return MapToDetailsResponse(screen);
    }

    public async Task SetScreenActiveAsync(string userId, bool isActive)
    {
        var screen = await db.Screens.FirstOrDefaultAsync(d => d.UserId == userId);
        if (screen == null)
        {
            logger.LogWarning(
                "No screen found for user {UserId} when setting active state",
                userId
            );
            return;
        }

        screen.IsActive = isActive;
        screen.LastSeenAt = DateTime.UtcNow;
        screen.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        logger.LogInformation(
            "Updated IsActive={IsActive} for screen {ScreenId}",
            isActive,
            screen.Id
        );
    }

    public async Task<int> GetScreenCountByStatusAsync(ApprovalStatus status)
    {
        return await db.Screens.CountAsync(d => d.ApprovalStatus == status);
    }

    public async Task<ScreenWithCampaignsResponse> GetScreenWithCampaignsAsync(Guid id)
    {
        var screen = await db
            .Screens.Include(d => d.CampaignAssignments)
                .ThenInclude(ca => ca.Campaign.CampaignAssets)
            .AsSplitQuery()
            .FirstOrDefaultAsync(d => d.Id == id);

        if (screen == null)
            throw new KeyNotFoundException($"Screen with ID {id} not found");

        var allCampaigns = await db
            .Campaigns.Include(c => c.CampaignAssets)
            .Include(c => c.CampaignAssignments)
            .AsSplitQuery()
            .OrderBy(c => c.Name)
            .ToListAsync();

        var utcNow = DateTime.UtcNow;
        var response = MapToDetailsResponse(screen);
        var assignments = screen
            .CampaignAssignments.Select(a => CampaignAssignmentPolicy.ToDetail(a, utcNow))
            .ToList();
        var allCampaignSummaries = MapCampaignSummaries(allCampaigns);

        return MapToScreenWithCampaignsResponse(response, assignments, allCampaignSummaries);
    }

    public async Task UpdateScreenWithCampaignsAsync(
        Guid id,
        UpdateScreenRequest request,
        List<CampaignAssignmentRequest> assignments
    )
    {
        var screen = await db.Screens.FindAsync(id);
        if (screen == null)
            throw new KeyNotFoundException($"Screen with ID {id} not found");

        if (!string.IsNullOrWhiteSpace(request.Name))
            screen.Name = request.Name;
        if (request.Description != null)
            screen.Description = request.Description;
        if (!string.IsNullOrWhiteSpace(request.Location))
            screen.Location = request.Location;

        if (request.ShufflePlayback.HasValue)
            screen.ShufflePlayback = request.ShufflePlayback.Value;

        screen.UpdatedAt = DateTime.UtcNow;

        // Validate that all requested campaigns exist before changing assignments
        var distinctCampaignIds = assignments.Select(a => a.CampaignId).Distinct().ToList();
        if (distinctCampaignIds.Count != assignments.Count)
            throw new ArgumentException("A campaign can only be assigned to a screen once");
        foreach (var assignment in assignments)
            CampaignAssignmentPolicy.Validate(assignment);

        if (distinctCampaignIds.Count > 0)
        {
            var existingCount = await db.Campaigns.CountAsync(c =>
                distinctCampaignIds.Contains(c.Id)
            );
            if (existingCount != distinctCampaignIds.Count)
                throw new ArgumentException("One or more campaigns do not exist");
        }

        // Update campaign assignments
        var currentAssignments = await db
            .CampaignAssignments.Where(ca =>
                ca.TargetKind == CampaignAssignmentTargetKind.Screen && ca.ScreenId == id
            )
            .ToListAsync();

        var toRemove = currentAssignments
            .Where(ca => !distinctCampaignIds.Contains(ca.CampaignId))
            .ToList();
        db.CampaignAssignments.RemoveRange(toRemove);

        var assignmentsByCampaign = currentAssignments.ToDictionary(a => a.CampaignId);
        foreach (var requestAssignment in assignments)
        {
            if (
                !assignmentsByCampaign.TryGetValue(requestAssignment.CampaignId, out var assignment)
            )
            {
                assignment = new CampaignAssignment
                {
                    CampaignId = requestAssignment.CampaignId,
                    ScreenId = id,
                    TargetKind = CampaignAssignmentTargetKind.Screen,
                    CreatedAt = DateTime.UtcNow,
                };
                db.CampaignAssignments.Add(assignment);
            }

            CampaignAssignmentPolicy.Apply(assignment, requestAssignment);
        }

        await db.SaveChangesAsync();
        logger.LogInformation(
            "Screen {ScreenId} updated with {CampaignCount} campaigns",
            screen.Id,
            assignments.Count
        );

        await audit.LogAsync(
            "Updated",
            ScreenAuditEntity,
            screen.Id.ToString(),
            $"Updated screen '{screen.Name}' ({assignments.Count} campaign(s) assigned)"
        );

        await syncService.SyncScreenAsync(screen.Id);
    }

    public Task UpdateScreenCampaignAssignmentsAsync(
        Guid id,
        List<CampaignAssignmentRequest> assignments
    ) => UpdateScreenWithCampaignsAsync(id, new UpdateScreenRequest(), assignments);

    public async Task<List<ScreenDetailsResponse>> GetApprovedScreensAsync(bool includeOffline)
    {
        var query = db.Screens.Where(d => d.ApprovalStatus == ApprovalStatus.Approved);
        if (!includeOffline)
            query = query.Where(d => d.IsActive);

        var screens = await query.OrderByDescending(d => d.CreatedAt).ToListAsync();
        return screens.Select(MapToDetailsResponse).ToList();
    }

    public async Task<bool> SendCommandAsync(Guid id, string command)
    {
        if (!ScreenCommands.IsValid(command))
            throw new ArgumentException($"Unknown screen command '{command}'.", nameof(command));

        var screen = await db.Screens.FirstOrDefaultAsync(d => d.Id == id);
        if (screen == null)
            throw new KeyNotFoundException($"Screen with ID {id} not found");

        var delivered = await syncService.SendCommandAsync(id, command);
        await audit.LogAsync(
            "Command",
            ScreenAuditEntity,
            id.ToString(),
            $"Sent command '{command}' to screen '{screen.Name}'{(delivered ? "" : " (screen offline)")}"
        );
        return delivered;
    }

    private static ScreenWithCampaignsResponse MapToScreenWithCampaignsResponse(
        ScreenDetailsResponse response,
        List<CampaignAssignmentDetail> assignments,
        List<CampaignSummary> allCampaigns
    )
    {
        return new ScreenWithCampaignsResponse
        {
            Id = response.Id,
            Name = response.Name,
            Description = response.Description,
            Location = response.Location,
            ScreenIdentifier = response.ScreenIdentifier,
            ApprovalStatus = response.ApprovalStatus,
            UserId = response.UserId,
            ResolutionWidth = response.ResolutionWidth,
            ResolutionHeight = response.ResolutionHeight,
            IsActive = response.IsActive,
            LastSeenAt = response.LastSeenAt,
            ShufflePlayback = response.ShufflePlayback,
            CreatedAt = response.CreatedAt,
            UpdatedAt = response.UpdatedAt,
            CampaignAssignments = assignments,
            AllCampaigns = allCampaigns,
        };
    }

    private static List<CampaignSummary> MapCampaignSummaries(
        IEnumerable<Database.Models.Campaign> campaigns
    )
    {
        return campaigns
            .Select(c => new CampaignSummary(
                c.Id,
                c.Name,
                c.Description,
                c.CampaignAssets.Count,
                c.CampaignAssignments.Count(a =>
                    a.TargetKind == CampaignAssignmentTargetKind.Screen
                ),
                c.CreatedAt,
                c.UpdatedAt,
                c.CampaignAssignments.Count(a => a.IsActiveAt(DateTime.UtcNow)),
                c.CampaignAssignments.Any(a =>
                    a.TargetKind == CampaignAssignmentTargetKind.GlobalFallback
                )
            ))
            .ToList();
    }

    private static ScreenDetailsResponse MapToDetailsResponse(Screen screen)
    {
        return new ScreenDetailsResponse
        {
            Id = screen.Id,
            Name = screen.Name,
            Description = screen.Description,
            Location = screen.Location,
            ScreenIdentifier = screen.ScreenIdentifier,
            ApprovalStatus = screen.ApprovalStatus.ToString(),
            UserId = screen.UserId,
            ResolutionWidth = screen.ResolutionWidth,
            ResolutionHeight = screen.ResolutionHeight,
            IsActive = screen.IsActive,
            LastSeenAt = screen.LastSeenAt,
            ShufflePlayback = screen.ShufflePlayback,
            CreatedAt = screen.CreatedAt,
            UpdatedAt = screen.UpdatedAt,
        };
    }
}
