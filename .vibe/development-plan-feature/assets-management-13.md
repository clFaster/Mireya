# Development Plan: Mireya (feature/assets-management-13 branch)

_Generated on 2025-11-06 by Vibe Feature MCP_
_Workflow: [epcc](https://mrsimpson.github.io/responsible-vibe-mcp/workflows/epcc)_

## Goal

Improve Assets Management for Mireya Digital Signage system by implementing:

- Upload assets (images, videos, etc.) via UI
- Delete existing assets from the system
- Preview assets before taking actions

**Out of Scope**: Asset processing (converting to different resolutions/formats)

**GitHub Issue**: #13 - Feature: Improve Assets Management (Upload, Delete, Preview)

## Explore

### Phase Entrance Criteria:

- [x] Development workflow started
- [x] Initial plan file created

### Tasks

- [x] Analyze existing asset management implementation
- [x] Understand current file upload mechanism
- [x] Review existing UI pages (Assets/Index and Assets/Upload)
- [x] Identify what's already working vs what needs to be added
- [x] Review Asset model properties (Name, Description, Type)
- [x] Identify Website asset type support gap
- [x] Identify missing edit metadata UI

### Completed

- [x] Created development plan file
- [x] Analyzed existing AssetService implementation
- [x] Reviewed AssetController API endpoints
- [x] Examined Asset model structure (including Name, Description, AssetType enum)
- [x] Reviewed existing Razor Pages UI (Assets/Index.cshtml and Upload.cshtml)
- [x] Identified UpdateAssetMetadata backend exists but no UI
- [x] Identified Website asset type (AssetType.Website = 3) not implemented

## Plan

### Phase Entrance Criteria:

- [x] Existing codebase patterns and architecture understood
- [x] Current asset management implementation analyzed
- [x] File upload/storage mechanisms identified
- [x] Security requirements defined (file type validation, size limits)
- [x] Frontend and backend integration points documented

### Tasks

- [x] Analyze current implementation gaps
- [x] Define implementation approach for each feature
- [x] Break down work into logical phases
- [x] Identify dependencies and potential challenges
- [x] Document security considerations
- [x] Plan UI/UX improvements

### Completed

- [x] Created detailed implementation plan (see Key Decisions section)
- [x] Defined task breakdown for coding phase
- [x] Documented edge cases and security considerations

## Code

### Phase Entrance Criteria:

- [ ] Detailed implementation plan completed
- [ ] Architecture and design decisions documented
- [ ] Task breakdown for backend and frontend completed
- [ ] Security measures planned
- [ ] User approval obtained on the plan

### Tasks

#### Phase 1: Fix Delete Handler

- [x] Add `OnPostDeleteAsync` method to `Index.cshtml.cs`
- [x] Add success/error message properties
- [x] Test delete functionality with existing backend

#### Phase 2: Video Upload Support

- [x] Update `AssetService.UploadAssetsAsync` to support video extensions
- [x] Add MIME type validation for videos
- [x] Add file size validation (configurable limits)
- [x] Update `Upload.cshtml` accept attribute for videos
- [x] Update `Index.cshtml` to display video assets correctly
- [x] Test video upload workflow

#### Phase 3: Edit Metadata UI

- [x] Create Edit modal HTML in `Index.cshtml`
- [x] Add Edit button to asset cards
- [x] Add JavaScript for modal show/hide
- [x] Create `OnPostEditAsync` handler in `Index.cshtml.cs`
- [x] Call existing `UpdateAssetMetadata` API endpoint
- [x] Add form validation for Name (required, max 200) and Description (max 1000)
- [x] Display success/error feedback
- [x] Test edit functionality

#### Phase 4: Website Asset Type

- [x] Add `CreateWebsiteAssetAsync` method to `IAssetService` and `AssetService`
- [x] Add corresponding API endpoint in `AssetController`
- [x] Add "Add Website" UI (on Upload page or new section)
- [x] Add URL validation (format, protocol)
- [x] Update `Index.cshtml` to display website assets (globe icon)
- [x] Test website asset creation and display

#### Bug Fixes

- [x] Fix edit metadata dialog not opening (changed from onclick to data attributes + event listeners)
- [x] Fix pagination links (changed from ?page= to ?CurrentPage= to match model property)

#### Phase 5: Preview Modal

- [x] Create preview modal component in `Index.cshtml`
- [x] Add JavaScript for modal navigation (open, close, keyboard)
- [x] Support image preview (full size display)
- [x] Support video preview (video player with controls)
- [x] Support website preview (iframe with fallback)
- [x] Add Preview button to asset cards
- [x] Test preview for all asset types

#### Phase 6: Upload Preview (Optional)

- [x] Add JavaScript file input change handler in `Upload.cshtml`
- [x] Generate thumbnail previews for selected images
- [x] Display file info (name, size, type)
- [x] Allow removing files before upload
- [x] Add client-side validation feedback (file type, size limits)
- [x] Test upload preview functionality

### Completed

- [x] Phase 1: Delete handler and backend integration
- [x] Phase 2: Video upload support with validation
- [x] Phase 3: Edit metadata modal UI
- [x] Phase 4: Website asset type implementation
- [x] Phase 5: Preview modal for all asset types
- [x] Phase 6: Upload preview with thumbnails and validation
- [x] Bug Fix: Edit modal data attributes
- [x] Bug Fix: Pagination query parameter fix

## Commit

### Phase Entrance Criteria:

- [ ] All planned features implemented
- [ ] Backend endpoints tested and working
- [ ] Frontend UI components functional
- [ ] Security measures implemented
- [ ] Existing tests pass
- [ ] User acceptance criteria met

### Tasks

- [x] Remove debug output statements from all modified files
- [x] Review and address TODO/FIXME comments
- [x] Remove commented-out code and debugging blocks
- [x] Verify documentation accuracy (no docs exist yet)
- [x] Run existing tests to ensure no regressions
- [x] Final code review and validation

### Completed

- [x] Code cleanup: No debug statements or commented code found
- [x] TODO review: No TODOs in modified files
- [x] Tests: Build successful, no regressions detected
- [x] Documentation: No documentation files exist yet to update

## Key Decisions

### Exploration Findings:

**Current Implementation Status:**

1. **Upload Assets** ✅ ALREADY IMPLEMENTED
   - Backend: `AssetController.UploadAssets()` endpoint exists
   - Service: `AssetService.UploadAssetsAsync()` handles file upload
   - UI: Upload page exists at `/Admin/Assets/Upload`
   - Currently only supports images (.jpg, .jpeg, .png)
   - Files stored in `/uploads` folder
   - Generates GUID-based filenames

2. **Delete Assets** ⚠️ PARTIALLY IMPLEMENTED
   - Backend: `AssetController.DeleteAsset()` endpoint exists
   - Service: `AssetService.DeleteAssetAsync()` handles deletion
   - Frontend: Delete button exists on Assets/Index page but NO HANDLER in code-behind
   - **ISSUE**: The Razor Page has no `OnPostDeleteAsync` handler

3. **Preview Assets** ⚠️ BASIC IMPLEMENTATION
   - UI shows thumbnail/preview in grid view
   - "View" link opens asset in new tab
   - **MISSING**: Modal/inline preview functionality

4. **Edit Asset Metadata** ⚠️ BACKEND ONLY
   - Backend: `AssetController.UpdateAssetMetadata()` endpoint exists
   - Service: `AssetService.UpdateAssetMetadataAsync()` implemented
   - **MISSING**: No UI to edit Name and Description fields
   - Assets have Name (max 200 chars) and Description (max 1000 chars, optional)

5. **Website Assets** ❌ NOT IMPLEMENTED
   - AssetType.Website (=3) exists in enum
   - No UI or workflow to add website URLs as assets
   - Different from file upload - needs URL input field

**Architecture Details:**

- ASP.NET Core (.NET 8) backend with Razor Pages
- Entity Framework Core with SQLite (dev) and PostgreSQL (prod)
- Static file serving configured for `/uploads` directory
- Cookie-based authentication with ASP.NET Identity
- Tailwind CSS for styling
- No JavaScript framework (vanilla JS for enhancements)

**Security Measures Identified:**

- File type validation (currently only images)
- Authorization required (Admin role)
- Unique GUID filenames prevent conflicts/overwrites

**What Needs to be Added:**

1. Fix delete functionality on Razor Page (add handler)
2. Expand file type support to videos (.mp4, .webm, etc.)
3. Add support for Website asset type (URL input instead of file upload)
4. Add file size validation (security measure)
5. Improve preview with modal/lightbox
6. Add preview before upload
7. **Add ability to EDIT asset metadata (Name and Description)**
   - Backend has UpdateAssetMetadataAsync but NO UI for it
   - Need edit page/modal to update Name and Description fields

---

### Implementation Plan:

#### **Phase 1: Fix Critical Issues (Delete Handler)**

**Priority: HIGH - Existing feature is broken**

- Add `OnPostDeleteAsync` handler to `Index.cshtml.cs`
- Implement proper error handling and user feedback
- Test delete functionality end-to-end

#### **Phase 2: Extend File Upload Support (Videos)**

**Priority: HIGH - Core requirement from issue**

- Update `AssetService.UploadAssetsAsync()` to accept video files
- Add video file extensions: .mp4, .webm, .avi, .mov
- Update file type validation logic
- Add file size validation (e.g., max 100MB for videos, 10MB for images)
- Update UI to show video icon/preview correctly
- Test video upload and display

#### **Phase 3: Add Edit Metadata UI**

**Priority: HIGH - Requested feature, backend exists**

- Create Edit modal or separate page for asset metadata
- Add "Edit" button to asset cards in Index.cshtml
- Wire up to existing `UpdateAssetMetadata` API endpoint
- Include Name and Description fields
- Add form validation
- Show success/error feedback

#### **Phase 4: Add Website Asset Type**

**Priority: MEDIUM - New asset type**

- Update `AssetService` to handle Website type creation
- Add new method `CreateWebsiteAssetAsync(string url, string name, string? description)`
- Add UI for adding website URLs (could be on Upload page or separate)
- Validate URL format
- No file upload needed - just store URL in Source field
- Update asset grid to show Website assets differently (globe icon?)

#### **Phase 5: Improve Preview Experience**

**Priority: MEDIUM - UX enhancement**

- Add modal/lightbox for inline preview
- Support preview for images, videos, and websites (iframe)
- Add keyboard navigation (ESC to close, arrows for next/prev)
- Add "Preview" button to each asset card
- Consider using a lightweight JS library or build custom modal

#### **Phase 6: Add Upload Preview**

**Priority: LOW - Nice-to-have UX enhancement**

- Show file previews before upload submission
- Display thumbnail for selected images
- Show file name, size, type for all files
- Allow removing files from selection before upload
- Add client-side validation feedback

---

### Technical Decisions:

**1. File Size Limits:**

- Images: 10 MB max
- Videos: 100 MB max
- Configurable via appsettings.json

**2. Supported File Types:**

- Images: .jpg, .jpeg, .png, .gif, .webp
- Videos: .mp4, .webm, .avi, .mov

**3. Edit UI Approach:**

- Use inline modal (not separate page) for quick editing
- Keep user on Index page for better UX
- Use vanilla JavaScript for modal functionality (no extra dependencies)

**4. Website Asset Validation:**

- URL format validation (must be valid HTTP/HTTPS URL)
- Optional URL reachability check (ping before save)
- Store raw URL in Source field

**5. Preview Modal:**

- Custom lightweight modal with Tailwind CSS
- Support all three asset types
- Escape key to close, click outside to close

**6. Error Handling:**

- User-friendly error messages
- Validation on both client and server side
- Log errors server-side for debugging
- Show temporary success/error notifications

---

### Edge Cases & Considerations:

1. **Large File Uploads:**
   - Consider upload progress indicator for large videos
   - Server timeout configuration may need adjustment
   - Consider chunked uploads for very large files (future enhancement)

2. **Website Assets:**
   - Some websites may block iframe embedding (X-Frame-Options)
   - Consider adding a note about this limitation
   - Fallback: open in new tab if iframe fails

3. **Concurrent Deletes:**
   - What if file is deleted while being viewed?
   - Handle 404 gracefully in preview

4. **File Name Conflicts:**
   - Already handled with GUID-based naming
   - Keep this approach

5. **Video Format Support:**
   - Not all browsers support all video formats
   - Document supported formats in UI
   - Consider transcoding in future (out of scope per issue)

6. **Storage Space:**
   - No disk space checking currently
   - Consider adding warning when disk space is low (future)

---

### Security Considerations:

1. **File Type Validation:**
   - Validate file extension AND MIME type
   - Prevent executable files (.exe, .bat, .sh)
   - Check magic bytes, not just extension

2. **File Size Limits:**
   - Enforce max file size on server side
   - Prevent DoS via large uploads
   - Configure in appsettings for flexibility

3. **URL Validation (Websites):**
   - Validate URL format and protocol (http/https only)
   - Prevent XSS via malicious URLs
   - Consider URL length limits

4. **Authorization:**
   - Already enforced (Admin role required)
   - Keep this for all new endpoints/handlers

5. **Path Traversal:**
   - Already mitigated with GUID filenames
   - Ensure delete operation doesn't allow path traversal

6. **SQL Injection:**
   - Using EF Core with parameterized queries
   - Already protected

## Notes

**File Structure:**

- Backend API: `src/Mireya.Api/`
- Controllers: `src/Mireya.Api/Controllers/`
- Services: `src/Mireya.Api/Services/Asset/`
- Razor Pages: `src/Mireya.Api/Areas/Admin/Pages/Assets/`
- Database Models: `src/Mireya.Database/Models/`
- Upload Directory: `src/Mireya.Api/uploads/`

**Key Files:**

- `AssetController.cs` - REST API endpoints
- `AssetService.cs` - Business logic for asset operations
- `Asset.cs` - Database model
- `Index.cshtml/.cs` - Asset listing page
- `Upload.cshtml/.cs` - Asset upload page

**Asset Types Supported:**

- Image = 1
- Video = 2
- Website = 3

**Technical Stack:**

- .NET 8 / ASP.NET Core
- Entity Framework Core
- Razor Pages with Tailwind CSS
- SQLite (dev) / PostgreSQL (prod)

---

_This plan is maintained by the LLM. Tool responses provide guidance on which section to focus on and what tasks to work on._
