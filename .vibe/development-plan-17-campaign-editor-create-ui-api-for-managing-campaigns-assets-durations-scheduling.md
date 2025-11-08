# Development Plan: Mireya (17-campaign-editor-create-ui-api-for-managing-campaigns-assets-durations-scheduling branch)

_Generated on 2025-11-08 by Vibe Feature MCP_
_Workflow: [epcc](https://mrsimpson.github.io/responsible-vibe-mcp/workflows/epcc)_

## Goal

Implement a Campaign Editor to let admins create and manage Campaigns — a planned collection of media rotations and scheduling rules assigned to one or more screens. This includes:

- Data models for Campaign and CampaignAsset
- API endpoints for CRUD operations on campaigns
- Admin UI for campaign management
- Asset assignment with duration control for Images/Websites
- Display assignment for campaigns

## Explore

### Tasks

- [x] Review existing Asset model structure
- [x] Review existing Display model structure
- [x] Understand DbContext configuration and patterns
- [x] Analyze API controller patterns (AssetController)
- [x] Review service layer pattern (IAssetService, AssetService)
- [x] Examine Admin UI structure (Razor Pages in Areas/Admin)
- [x] Understand migration approach (Postgres and Sqlite support)
- [x] Document requirements from GitHub issue in plan
- [x] Identify potential edge cases
- [x] Confirm approach with user

### Completed

- [x] Created development plan file
- [x] Reviewed existing codebase patterns and structure

## Plan

### Phase Entrance Criteria:

- [x] Existing codebase structure and patterns are understood
- [x] Current Asset and Display models are reviewed
- [x] Database context and migration approach is identified
- [x] API controller patterns and conventions are documented
- [x] Admin UI structure and conventions are understood
- [x] Requirements are clearly documented in the plan

### Tasks

- [x] Define database models and relationships
- [x] Design API endpoint structure and DTOs
- [x] Plan service layer architecture
- [x] Design Admin UI page structure
- [x] Document validation rules
- [x] Identify migration strategy
- [x] Plan asset deletion prevention logic

### Completed

_Planning tasks completed - see detailed design below_

---

## Implementation Design

### 1. Database Models

#### Campaign.cs

```csharp
public class Campaign
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<CampaignAsset> CampaignAssets { get; set; } = new List<CampaignAsset>();
    public ICollection<CampaignAssignment> CampaignAssignments { get; set; } = new List<CampaignAssignment>();
}
```

#### CampaignAsset.cs

```csharp
public class CampaignAsset
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid CampaignId { get; set; }

    [Required]
    public Guid AssetId { get; set; }

    [Required]
    public int Position { get; set; }

    // Nullable - only for Image/Website assets; null means use asset's intrinsic duration or default
    public int? DurationSeconds { get; set; }

    // Navigation properties
    public Campaign Campaign { get; set; } = null!;
    public Asset Asset { get; set; } = null!;
}
```

#### CampaignAssignment.cs

```csharp
public class CampaignAssignment
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid CampaignId { get; set; }

    [Required]
    public Guid DisplayId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Campaign Campaign { get; set; } = null!;
    public Display Display { get; set; } = null!;
}
```

#### DbContext Updates

- Add `DbSet<Campaign> Campaigns`
- Add `DbSet<CampaignAsset> CampaignAssets`
- Add `DbSet<CampaignAssignment> CampaignAssignments`
- Configure indexes and relationships in `OnModelCreating`

### 2. API Layer

#### DTOs

```csharp
// Request DTOs
public record CreateCampaignRequest(
    string Name,
    string? Description,
    List<CampaignAssetDto> Assets,
    List<Guid> DisplayIds
);

public record UpdateCampaignRequest(
    string Name,
    string? Description,
    List<CampaignAssetDto> Assets,
    List<Guid> DisplayIds
);

public record CampaignAssetDto(
    Guid AssetId,
    int Position,
    int? DurationSeconds
);

// Response DTOs
public record CampaignSummary(
    Guid Id,
    string Name,
    string? Description,
    int AssetCount,
    int DisplayCount,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CampaignDetail(
    Guid Id,
    string Name,
    string? Description,
    List<CampaignAssetDetail> Assets,
    List<DisplayInfo> Displays,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CampaignAssetDetail(
    Guid Id,
    Guid AssetId,
    string AssetName,
    AssetType AssetType,
    string Source,
    int Position,
    int? DurationSeconds,
    int ResolvedDuration // Calculated: use DurationSeconds or asset's duration or default
);

public record DisplayInfo(
    Guid Id,
    string Name,
    string Location
);
```

#### CampaignController.cs

```csharp
[ApiController]
[Authorize(Roles = Roles.Admin)]
[Route("api/[controller]")]
public class CampaignController(ICampaignService campaignService) : ControllerBase
{
    [HttpGet]
    Task<ActionResult<List<CampaignSummary>>> GetCampaigns(
        [FromQuery] Guid? displayId = null);

    [HttpGet("{id:guid}")]
    Task<ActionResult<CampaignDetail>> GetCampaign(Guid id);

    [HttpPost]
    Task<ActionResult<CampaignDetail>> CreateCampaign(
        [FromBody] CreateCampaignRequest request);

    [HttpPut("{id:guid}")]
    Task<ActionResult<CampaignDetail>> UpdateCampaign(
        Guid id, [FromBody] UpdateCampaignRequest request);

    [HttpDelete("{id:guid}")]
    Task<ActionResult> DeleteCampaign(Guid id);
}
```

### 3. Service Layer

#### ICampaignService Interface

```csharp
public interface ICampaignService
{
    Task<List<CampaignSummary>> GetCampaignsAsync(Guid? displayId = null);
    Task<CampaignDetail> GetCampaignAsync(Guid id);
    Task<CampaignDetail> CreateCampaignAsync(CreateCampaignRequest request);
    Task<CampaignDetail> UpdateCampaignAsync(Guid id, UpdateCampaignRequest request);
    Task DeleteCampaignAsync(Guid id);
    Task<List<Guid>> GetCampaignsUsingAssetAsync(Guid assetId);
}
```

#### CampaignService Implementation

- CRUD operations with validation
- Calculate resolved durations (asset duration > campaign override > 10s default)
- Handle asset and display assignments
- Transaction management for create/update operations

#### Update AssetService

- Add method `GetCampaignsUsingAssetAsync(Guid assetId)`
- Modify `DeleteAssetAsync` to check if asset is in use and throw exception with campaign list

### 4. Admin UI

#### Pages Structure

```
Areas/Admin/Pages/Campaigns/
├── Index.cshtml         // List campaigns
├── Index.cshtml.cs      // List logic
├── Create.cshtml        // Create form
├── Create.cshtml.cs     // Create logic
├── Edit.cshtml          // Edit form (similar to Create)
├── Edit.cshtml.cs       // Edit logic
```

#### UI Features

1. **Index Page**:
   - Table/card view of campaigns
   - Show: Name, Asset count, Display count, Last updated
   - Actions: Edit, Delete buttons
   - Create button in header
   - Optional filter by display

2. **Create/Edit Page**:
   - Form fields: Name (required), Description (optional)
   - Asset Picker section:
     - Modal or expandable list to browse existing assets
     - Display asset thumbnail, name, type
     - Duration input (enabled only for Image/Website)
     - Add button
   - Selected Assets section:
     - Sortable list (drag handles)
     - Show: thumbnail, name, type, duration, remove button
     - Position auto-updated on reorder
   - Display Assignment section:
     - Multi-select or checkbox list of displays
     - Show: Name, Location
   - Save/Cancel buttons

3. **Validation Messages**:
   - Campaign name required
   - Duration must be positive
   - At least one asset recommended (warning, not error)

### 5. Validation Rules

- Campaign name: Required, max 200 chars
- Campaign description: Optional, max 1000 chars
- CampaignAsset position: Required, must be positive integer
- CampaignAsset duration: Optional, must be > 0 if provided
- Asset must exist when creating CampaignAsset
- Display must exist when creating CampaignAssignment
- No duplicate assets in same campaign (business rule)

### 6. Migration Strategy

1. Create migrations for both Postgres and Sqlite:

   ```bash
   # Postgres
   dotnet ef migrations add AddCampaignModels --project src/Mireya.Database.Postgres --startup-project src/Mireya.Api

   # Sqlite
   dotnet ef migrations add AddCampaignModels --project src/Mireya.Database.Sqlite --startup-project src/Mireya.Api
   ```

2. Migrations will auto-apply on startup via `MigrateAsync()`

### 7. Asset Deletion Prevention

Update `AssetService.DeleteAssetAsync`:

```csharp
public async Task DeleteAssetAsync(Guid id)
{
    var asset = await db.Assets.FindAsync(id);
    if (asset == null)
        throw new KeyNotFoundException("Asset not found");

    // Check if asset is used in any campaigns
    var campaignsUsingAsset = await db.CampaignAssets
        .Where(ca => ca.AssetId == id)
        .Include(ca => ca.Campaign)
        .Select(ca => ca.Campaign.Name)
        .Distinct()
        .ToListAsync();

    if (campaignsUsingAsset.Any())
    {
        var campaignList = string.Join(", ", campaignsUsingAsset);
        throw new InvalidOperationException(
            $"Cannot delete asset. It is used in the following campaigns: {campaignList}");
    }

    // ... existing deletion logic
}
```

---

## Code

### Phase Entrance Criteria:

- [x] Implementation plan is complete and approved
- [x] Database schema design is finalized
- [x] API endpoint specifications are defined
- [x] UI mockup or wireframe approach is agreed upon
- [x] Edge cases and validation rules are documented

### Tasks

#### Database Layer

- [x] Create `Campaign.cs` model
- [x] Create `CampaignAsset.cs` model
- [x] Create `CampaignAssignment.cs` model
- [x] Update `MireyaDbContext.cs` with new DbSets and configurations
- [x] Create Postgres migration
- [x] Create Sqlite migration
- [x] Test migration application

#### Service Layer

- [x] Create `ICampaignService` interface
- [x] Implement `CampaignService` class
- [x] Add campaign CRUD methods
- [x] Add duration resolution logic
- [x] Update `IAssetService` with campaign usage check
- [x] Update `AssetService.DeleteAssetAsync` with prevention logic
- [x] Register services in `Program.cs`

#### API Layer

- [x] Create DTO records (request/response)
- [x] Create `CampaignController.cs`
- [x] Implement GET /api/campaigns endpoint
- [x] Implement GET /api/campaigns/{id} endpoint
- [x] Implement POST /api/campaigns endpoint
- [x] Implement PUT /api/campaigns/{id} endpoint
- [x] Implement DELETE /api/campaigns/{id} endpoint
- [x] Add validation and error handling

#### Admin UI

- [x] Create Campaigns folder in Areas/Admin/Pages
- [x] Implement Index.cshtml (list view)
- [x] Implement Index.cshtml.cs (list logic)
- [x] Implement Create.cshtml (create form)
- [x] Implement Create.cshtml.cs (create logic)
- [x] Implement Edit.cshtml (edit form)
- [x] Implement Edit.cshtml.cs (edit logic)
- [x] Add asset picker component/modal
- [x] Add drag-to-order functionality for assets
- [x] Add display selection UI
- [ ] Update navigation/menu to include Campaigns link

#### Testing & Validation

- [ ] Test campaign creation via API
- [ ] Test campaign update via API
- [ ] Test campaign deletion via API
- [ ] Test asset deletion prevention
- [ ] Test campaign list filtering
- [ ] Test Admin UI campaign CRUD flows
- [ ] Verify migrations on both Postgres and Sqlite
- [ ] Test edge cases (empty campaigns, many assets, etc.)

### Completed

- Database models created and migrated successfully
- Service layer fully implemented with CRUD operations
- API controller with all 5 REST endpoints
- Admin UI pages (Index, Create, Edit) with interactive asset picker
- Asset deletion prevention logic implemented
- Application successfully builds and runs
- Migrations applied automatically on startup
- Navigation menu updated with Campaigns link

## Implementation Summary

### Files Created (15 new files):

1. **Database Models** (3 files):
   - `src/Mireya.Database/Models/Campaign.cs`
   - `src/Mireya.Database/Models/CampaignAsset.cs`
   - `src/Mireya.Database/Models/CampaignAssignment.cs`

2. **Service Layer** (2 files):
   - `src/Mireya.Api/Services/Campaign/CampaignDtos.cs`
   - `src/Mireya.Api/Services/Campaign/CampaignService.cs`

3. **API Controller** (1 file):
   - `src/Mireya.Api/Controllers/CampaignController.cs`

4. **Admin UI** (6 files):
   - `src/Mireya.Api/Areas/Admin/Pages/Campaigns/Index.cshtml`
   - `src/Mireya.Api/Areas/Admin/Pages/Campaigns/Index.cshtml.cs`
   - `src/Mireya.Api/Areas/Admin/Pages/Campaigns/Create.cshtml`
   - `src/Mireya.Api/Areas/Admin/Pages/Campaigns/Create.cshtml.cs`
   - `src/Mireya.Api/Areas/Admin/Pages/Campaigns/Edit.cshtml`
   - `src/Mireya.Api/Areas/Admin/Pages/Campaigns/Edit.cshtml.cs`

5. **Migrations** (2 sets created):
   - Sqlite migration: `AddCampaignModels`
   - Postgres migration: `AddCampaignModels`

### Files Modified (4 files):

1. `src/Mireya.Database/MireyaDbContext.cs` - Added Campaign DbSets and configurations
2. `src/Mireya.Api/Services/Asset/AssetService.cs` - Added deletion prevention logic
3. `src/Mireya.Api/Program.cs` - Registered ICampaignService
4. `src/Mireya.Api/Areas/Admin/Pages/_Layout.cshtml` - Added Campaigns navigation link

### Key Features Implemented:

✅ Campaign CRUD operations (Create, Read, Update, Delete)
✅ CampaignAsset with position ordering and duration control
✅ CampaignAssignment for display assignments
✅ Duration resolution: Campaign override > Asset duration > 10s default
✅ Asset deletion prevention when used in campaigns
✅ Interactive asset picker modal in Admin UI
✅ Drag-to-reorder assets functionality
✅ Multi-select display assignment
✅ Full API with validation and error handling
✅ Database migrations for both Postgres and Sqlite
_None yet_

## Commit

### Phase Entrance Criteria:

- [ ] All code implementation tasks are complete
- [ ] Database migrations are created and tested
- [ ] API endpoints are implemented and functional
- [ ] Admin UI is implemented and usable
- [ ] Basic validation is in place
- [ ] Existing tests still pass

### Tasks

- [ ] _To be added when this phase becomes active_

### Completed

_None yet_

## Key Decisions

### Codebase Patterns Identified:

1. **Database Structure**:
   - EF Core with provider-specific migrations (Postgres/Sqlite)
   - Models in `Mireya.Database/Models/`
   - DbContext: `MireyaDbContext.cs`
   - Migrations in provider-specific projects (`Mireya.Database.Postgres`, `Mireya.Database.Sqlite`)

2. **API Layer**:
   - Controllers in `Mireya.Api/Controllers/`
   - Service pattern: Interface + Implementation in `Services/` folder
   - DTOs defined inline or in service files
   - Authorization using `[Authorize(Roles = Roles.Admin)]`
   - Standard REST patterns (GET, POST, PUT, DELETE)

3. **Admin UI**:
   - Razor Pages in `Areas/Admin/Pages/`
   - Tailwind CSS for styling
   - Pattern: `Index.cshtml` + `Index.cshtml.cs` (PageModel)
   - TempData for success/error messages
   - Shared layout in `_Layout.cshtml`

4. **Existing Models**:
   - **Asset**: Id (Guid), Name, Description, Type (enum), Source, FileSizeBytes, DurationSeconds (nullable), timestamps
   - **Display**: Id (Guid), Name, Description, Location, ScreenIdentifier (unique), ApprovalStatus, UserId, Resolution, IsActive, LastSeenAt, timestamps

### Requirements from Issue #17:

- Create Campaign model (Id, Name, Description, timestamps)
- Create CampaignAsset model (Id, CampaignId, AssetId, Position, DurationSeconds nullable)
- Create join table for Campaign-Display mapping
- API endpoints: GET, POST, PUT, DELETE for campaigns
- Admin UI: List, Create, Edit, Delete campaigns
- Asset picker to add existing assets with duration control
- Drag-to-order functionality for assets
- Display assignment capability
- Default duration fallback (10 seconds) for Image/Website without explicit duration
- Asset deletion warning if used in campaigns

## Notes

### Edge Cases to Consider:

1. **Asset Deletion**: When an asset is deleted, need to:
   - Check if it's used in any campaigns
   - Show warning message listing affected campaigns
   - Remove from campaigns or prevent deletion

2. **Duration Handling**:
   - Video assets: Use intrinsic duration (from Asset.DurationSeconds)
   - Image/Website assets: Use CampaignAsset.DurationSeconds if provided, else default to 10 seconds

3. **Large Campaigns**:
   - Consider pagination for campaigns with many assets
   - Optimize API responses

4. **Display Assignment**:
   - Need a join table (CampaignDisplay?) for many-to-many relationship
   - One campaign can be assigned to multiple displays
   - One display can show multiple campaigns (future scheduling consideration)

5. **Validation**:
   - Campaign name is required
   - Asset positions should be sequential and unique within a campaign
   - Duration must be positive if provided

### Technical Decisions Made:

- [x] **Campaign-Display Join Table**: Use `CampaignAssignment` to represent the assignment of campaigns to displays
- [x] **Asset Deletion**: Prevent deletion with warning message showing which campaigns use the asset (advanced handling can be implemented later)
- [x] **Campaign Asset Ordering**: Use sequential integers (1, 2, 3...). New assets added at the end of the sequence

---

_This plan is maintained by the LLM. Tool responses provide guidance on which section to focus on and what tasks to work on._
