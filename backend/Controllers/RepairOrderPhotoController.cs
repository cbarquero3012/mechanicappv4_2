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
    /// Manages photo uploads, retrieval, authenticated download, and deletion for repair orders.
    /// Photos are stored under a tenant-and-user-scoped path so that files from different
    /// tenants or users are always physically isolated:
    /// <c>wwwroot/uploads/images_{tenantSlug}_{username}/orders/{repairOrderId}/</c>.
    /// </summary>
    /// <remarks>
    /// All endpoints require a valid JWT (<see cref="AuthorizeAttribute"/>).
    /// The <c>X-Tenant-Slug</c> header is resolved by <c>TenantResolutionMiddleware</c>
    /// before any action runs.
    /// </remarks>
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
        /// Retains ASCII letters, digits, and hyphens; replaces every other character
        /// (including <c>/</c>, <c>\</c>, <c>..</c>, spaces) with an underscore.
        /// </summary>
        /// <param name="segment">The raw value to sanitize (e.g. a tenant slug or username).</param>
        /// <returns>A filesystem-safe, non-empty segment string.</returns>
        public static string SanitizeSegment(string segment) =>
            Regex.Replace(segment, @"[^a-zA-Z0-9\-]", "_");

        /// <summary>
        /// Returns all photos attached to a repair order, ordered newest-first.
        /// </summary>
        /// <param name="repairOrderId">The repair order whose photos to retrieve.</param>
        /// <returns>A JSON array of <see cref="RepairOrderPhoto"/> records.</returns>
        [HttpGet("{repairOrderId:int}")]
        [ProducesResponseType<IEnumerable<RepairOrderPhoto>>(StatusCodes.Status200OK)]
        public async Task<IActionResult> Get(int repairOrderId)
        {
            var result = await db.GetAll<RepairOrderPhoto>(
                @"SELECT * FROM mechanic_db.""RepairOrderPhotos""
                  WHERE ""RepairOrderId"" = @RepairOrderId
                  ORDER BY ""CreatedAt"" DESC",
                new { RepairOrderId = repairOrderId }).ConfigureAwait(false);

            return Ok(result);
        }

        /// <summary>
        /// Streams a single photo file back to the caller as <c>image/jpeg</c>.
        /// The original filename is preserved in the <c>Content-Disposition</c> header so
        /// that browsers and the Web Share API can use it when naming the attachment.
        /// </summary>
        /// <param name="id">The primary key of the <see cref="RepairOrderPhoto"/> record.</param>
        /// <returns>
        /// <list type="bullet">
        ///   <item><description>200 OK — JPEG file stream.</description></item>
        ///   <item><description>404 Not Found — record or file missing / invalid path.</description></item>
        /// </list>
        /// </returns>
        [HttpGet("download/{id:int}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(FileResult))]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Download(int id)
        {
            var photo = await db.GetAsync<RepairOrderPhoto>(
                @"SELECT * FROM mechanic_db.""RepairOrderPhotos"" WHERE ""Id"" = @Id",
                new { Id = id }).ConfigureAwait(false);

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

            // Include the original file name so the browser / share sheet can
            // label the attachment correctly (e.g. "20260601_abc12345.jpg").
            var safeFileName = Path.GetFileName(photo.FilePath);
            Response.Headers["Content-Disposition"] =
                $"inline; filename=\"{safeFileName}\"";

            return PhysicalFile(fullPath, "image/jpeg", enableRangeProcessing: false);
        }

        /// <summary>
        /// Uploads one or more JPG/JPEG photos and attaches them to a repair order.
        /// Each file is validated for extension and size, then saved to the
        /// tenant-and-user-scoped folder. Files that fail validation are listed in
        /// the <c>rejected</c> response array rather than causing the whole request to fail.
        /// </summary>
        /// <param name="repairOrderId">The repair order to attach the photos to.</param>
        /// <param name="files">One or more JPG/JPEG files (max 5 MB each).</param>
        /// <param name="description">Optional description applied to every uploaded photo.</param>
        /// <returns>
        /// <list type="bullet">
        ///   <item><description>200 OK — at least one photo was saved; response includes <c>photos</c> and <c>rejected</c> lists.</description></item>
        ///   <item><description>400 Bad Request — no files in request.</description></item>
        ///   <item><description>422 Unprocessable Entity — every file was rejected.</description></item>
        /// </list>
        /// </returns>
        [EnableRateLimiting("upload")]
        [HttpPost("{repairOrderId:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Upload(
            int repairOrderId,
            [FromForm] List<IFormFile> files,
            [FromForm] string? description)
        {
            if (files == null || files.Count == 0)
                return BadRequest(new { message = "No files provided." });

            var slug      = SanitizeSegment(tenantContext.CurrentTenant?.Slug ?? "shared");
            var username  = SanitizeSegment(User.FindFirst(ClaimTypes.Name)?.Value ?? "unknown");
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

                var relativeUrl = await fileStorage
                    .SaveFileAsync(file, subFolder, AllowedFileExtensions.Photos, 5 * 1024 * 1024)
                    .ConfigureAwait(false);

                var fileName = Path.GetFileName(relativeUrl);

                var photo = await db.GetAsync<RepairOrderPhoto>(
                    @"INSERT INTO mechanic_db.""RepairOrderPhotos""
                          (""RepairOrderId"", ""FileName"", ""FilePath"", ""Description"")
                      VALUES (@RepairOrderId, @FileName, @FilePath, @Description)
                      RETURNING *",
                    new
                    {
                        RepairOrderId = repairOrderId,
                        FileName      = fileName,
                        FilePath      = relativeUrl,
                        Description   = description,
                    }).ConfigureAwait(false);

                if (photo != null)
                    uploaded.Add(photo);
            }

            if (uploaded.Count == 0 && rejected.Count > 0)
                return UnprocessableEntity(new { message = "All files were rejected.", rejected });

            return Ok(new { message = $"{uploaded.Count} photo(s) uploaded.", photos = uploaded, rejected });
        }

        /// <summary>
        /// Deletes a photo record and its file from disk.
        /// </summary>
        /// <param name="id">The primary key of the photo to delete.</param>
        /// <returns>200 OK on success; 404 Not Found when the record does not exist.</returns>
        [HttpDelete("{id:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(int id)
        {
            var photo = await db.GetAsync<RepairOrderPhoto>(
                @"SELECT * FROM mechanic_db.""RepairOrderPhotos"" WHERE ""Id"" = @Id",
                new { Id = id }).ConfigureAwait(false);

            if (photo == null)
                return NotFound(new { message = "Photo not found." });

            fileStorage.DeleteFile(photo.FilePath);

            await db.EditData(
                @"DELETE FROM mechanic_db.""RepairOrderPhotos"" WHERE ""Id"" = @Id",
                new { Id = id }).ConfigureAwait(false);

            return Ok(new { message = "Photo deleted." });
        }
    }
}
