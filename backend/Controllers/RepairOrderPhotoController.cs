using System.Security.Claims;
using System.Text.RegularExpressions;
using MechanicApp.Server.Constants;
using MechanicApp.Server.Models;
using MechanicApp.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MechanicApp.Server.Controllers
{
    /// <summary>
    /// Manages photo uploads, retrieval, download, and deletion for repair orders.
    /// Photos are stored in a tenant-and-user-scoped folder:
    /// <c>uploads/images_{tenantSlug}_{username}/orders/{repairOrderId}/</c>.
    /// </summary>
    [Authorize]
    [EnableRateLimiting("authenticated")]
    [ApiController]
    [Route("api/[controller]")]
    public class RepairOrderPhotoController(
        IDbService db,
        IFileStorageService fileStorage,
        ITenantContext tenantContext) : ControllerBase
    {
        /// <summary>
        /// Sanitizes a path segment so it is safe to use as a filesystem directory name.
        /// Retains letters, digits, and hyphens; replaces all other characters with an underscore.
        /// </summary>
        /// <param name="segment">The raw segment (e.g. a tenant slug or username).</param>
        /// <returns>A filesystem-safe segment string.</returns>
        public static string SanitizeSegment(string segment) =>
            Regex.Replace(segment, @"[^a-zA-Z0-9\-]", "_");

        /// <summary>Get all photos for a repair order.</summary>
        /// <param name="repairOrderId">The repair order ID whose photos to retrieve.</param>
        [HttpGet("{repairOrderId}")]
        public async Task<IActionResult> Get(int repairOrderId)
        {
            var result = await db.GetAll<RepairOrderPhoto>(
                @"SELECT * FROM mechanic_db.""RepairOrderPhotos""
                  WHERE ""RepairOrderId"" = @RepairOrderId
                  ORDER BY ""CreatedAt"" DESC",
                new { RepairOrderId = repairOrderId });
            return Ok(result);
        }

        /// <summary>
        /// Downloads a single photo as a JPEG stream.
        /// Requires a valid JWT; the photo must belong to the caller's tenant.
        /// </summary>
        /// <param name="id">The photo record ID.</param>
        [HttpGet("download/{id:int}")]
        public async Task<IActionResult> Download(int id)
        {
            var photo = await db.GetAsync<RepairOrderPhoto>(
                @"SELECT * FROM mechanic_db.""RepairOrderPhotos"" WHERE ""Id"" = @Id",
                new { Id = id });

            if (photo == null)
                return NotFound(new { message = "Photo not found." });

            string fullPath;
            try
            {
                fullPath = fileStorage.GetFullPath(photo.FilePath);
            }
            catch (ArgumentException)
            {
                return NotFound(new { message = "Photo path is invalid." });
            }

            if (!System.IO.File.Exists(fullPath))
                return NotFound(new { message = "Photo file not found on disk." });

            return PhysicalFile(fullPath, "image/jpeg", enableRangeProcessing: false);
        }

        /// <summary>
        /// Upload one or more photos for a repair order.
        /// Files are saved to <c>uploads/images_{tenantSlug}_{username}/orders/{repairOrderId}/</c>.
        /// </summary>
        /// <param name="repairOrderId">The repair order to attach photos to.</param>
        /// <param name="files">One or more JPG/JPEG files (max 5 MB each).</param>
        /// <param name="description">Optional shared description for all uploaded photos.</param>
        [EnableRateLimiting("upload")]
        [HttpPost("{repairOrderId}")]
        public async Task<IActionResult> Upload(int repairOrderId, [FromForm] List<IFormFile> files, [FromForm] string? description)
        {
            if (files == null || files.Count == 0)
                return BadRequest("No files provided");

            var slug = SanitizeSegment(tenantContext.CurrentTenant?.Slug ?? "shared");
            var username = SanitizeSegment(User.FindFirst(ClaimTypes.Name)?.Value ?? "unknown");
            var subFolder = $"images_{slug}_{username}/orders/{repairOrderId}";

            var uploaded = new List<RepairOrderPhoto>();
            var rejected = new List<string>();

            foreach (var file in files)
            {
                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!AllowedFileExtensions.Photos.Contains(ext))
                {
                    rejected.Add(file.FileName);
                    continue;
                }

                if (file.Length > 5 * 1024 * 1024)
                {
                    rejected.Add(file.FileName);
                    continue;
                }

                var relativeUrl = await fileStorage.SaveFileAsync(
                    file, subFolder,
                    AllowedFileExtensions.Photos, 5 * 1024 * 1024);

                var fileName = Path.GetFileName(relativeUrl);

                var photo = await db.GetAsync<RepairOrderPhoto>(
                    @"INSERT INTO mechanic_db.""RepairOrderPhotos"" (""RepairOrderId"", ""FileName"", ""FilePath"", ""Description"")
                      VALUES (@RepairOrderId, @FileName, @FilePath, @Description)
                      RETURNING *",
                    new { RepairOrderId = repairOrderId, FileName = fileName, FilePath = relativeUrl, Description = description });

                if (photo != null)
                    uploaded.Add(photo);
            }

            if (uploaded.Count == 0 && rejected.Count > 0)
                return UnprocessableEntity(new { message = "All files were rejected.", rejected });

            return Ok(new { message = $"{uploaded.Count} photo(s) uploaded", photos = uploaded, rejected });
        }

        /// <summary>Delete a photo by ID.</summary>
        /// <param name="id">The photo record ID to delete.</param>
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var photo = await db.GetAsync<RepairOrderPhoto>(
                @"SELECT * FROM mechanic_db.""RepairOrderPhotos"" WHERE ""Id"" = @Id",
                new { Id = id });

            if (photo == null)
                return NotFound(new { message = "Photo not found" });

            fileStorage.DeleteFile(photo.FilePath);

            await db.EditData(
                @"DELETE FROM mechanic_db.""RepairOrderPhotos"" WHERE ""Id"" = @Id",
                new { Id = id });

            return Ok(new { message = "Photo deleted" });
        }
    }
}
