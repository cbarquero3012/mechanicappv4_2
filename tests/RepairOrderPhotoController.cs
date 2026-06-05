// ─────────────────────────────────────────────────────────────────────────────
// UNIT TESTS — RepairOrderPhotoController (WhatsApp image-sharing feature)
// ─────────────────────────────────────────────────────────────────────────────
//
//  SlugSanitizationTests          — verifies SanitizeSegment() produces
//      filesystem-safe strings and blocks path-traversal characters.
//
//  DownloadEndpointTests          — verifies Download() returns 404 when the
//      photo record or the file on disk is missing.
//
//  TenantUserPhotoStorageTests    — verifies Upload() passes the expected
//      tenant+user-scoped subfolder to IFileStorageService.
//
// ─────────────────────────────────────────────────────────────────────────────

using System.Security.Claims;
using MechanicApp.Server.Controllers;
using MechanicApp.Server.Models;
using MechanicApp.Server.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace MechanicApp.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// SanitizeSegment — input validation
// ─────────────────────────────────────────────────────────────────────────────
public class SlugSanitizationTests
{
    [Theory]
    [InlineData("my-tenant", "my-tenant")]          // already safe
    [InlineData("My Tenant", "My_Tenant")]           // spaces → underscore
    [InlineData("joes_taller", "joes_taller")]        // underscores kept
    [InlineData("../evil", "___evil")]             // path-traversal blocked
    [InlineData("a/b\\c", "a_b_c")]               // slashes blocked
    [InlineData("café", "caf_")]                // non-ASCII blocked
    [InlineData("", "")]                    // empty passthrough
    public void SanitizeSegment_ReturnsExpectedOutput(string input, string expected)
    {
        var result = RepairOrderPhotoController.SanitizeSegment(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void SanitizeSegment_DoesNotContainSlashOrDot()
    {
        var malicious = "../../etc/passwd";
        var result = RepairOrderPhotoController.SanitizeSegment(malicious);

        Assert.DoesNotContain("/", result);
        Assert.DoesNotContain("\\", result);
        Assert.DoesNotContain("..", result);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Download — 404 scenarios
// ─────────────────────────────────────────────────────────────────────────────
public class DownloadEndpointTests
{
    private static RepairOrderPhotoController BuildController(
        IDbService db, IFileStorageService fileStorage, ITenantContext tenantContext)
    {
        var controller = new RepairOrderPhotoController(db, fileStorage, tenantContext);

        // Set up an authenticated HttpContext with a minimal identity.
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, "test-user"),
            new Claim(ClaimTypes.Role, "Owner"),
        ], "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user },
        };

        return controller;
    }

    [Fact]
    public async Task Download_ReturnsNotFound_WhenPhotoRecordMissing()
    {
        // Arrange
        var db = Substitute.For<IDbService>();
        db.GetAsync<RepairOrderPhoto>(Arg.Any<string>(), Arg.Any<object>())
            .Returns((RepairOrderPhoto?)null);

        var fileStorage = Substitute.For<IFileStorageService>();
        var tenantCtx = Substitute.For<ITenantContext>();

        var sut = BuildController(db, fileStorage, tenantCtx);

        // Act
        var result = await sut.Download(999);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Download_ReturnsNotFound_WhenFileNotOnDisk()
    {
        // Arrange — photo record exists, but GetFullPath throws (path traversal guard)
        var db = Substitute.For<IDbService>();
        db.GetAsync<RepairOrderPhoto>(Arg.Any<string>(), Arg.Any<object>())
            .Returns(new RepairOrderPhoto { Id = 1, RepairOrderId = 1, FilePath = "/uploads/test/photo.jpg", FileName = "photo.jpg" });

        var fileStorage = Substitute.For<IFileStorageService>();
        // Return a path that does NOT exist on disk (temp path that won't be created)
        fileStorage.GetFullPath(Arg.Any<string>())
            .Returns(Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}.jpg"));

        var tenantCtx = Substitute.For<ITenantContext>();

        var sut = BuildController(db, fileStorage, tenantCtx);

        // Act
        var result = await sut.Download(1);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Download_ReturnsNotFound_WhenGetFullPathThrowsArgumentException()
    {
        // Arrange — GetFullPath throws (path-traversal protection in FileStorageService)
        var db = Substitute.For<IDbService>();
        db.GetAsync<RepairOrderPhoto>(Arg.Any<string>(), Arg.Any<object>())
            .Returns(new RepairOrderPhoto { Id = 2, RepairOrderId = 1, FilePath = "../../etc/passwd", FileName = "passwd" });

        var fileStorage = Substitute.For<IFileStorageService>();
        fileStorage.GetFullPath(Arg.Any<string>())
            .Returns(_ => throw new ArgumentException("Invalid file path."));

        var tenantCtx = Substitute.For<ITenantContext>();

        var sut = BuildController(db, fileStorage, tenantCtx);

        // Act
        var result = await sut.Download(2);

        // Assert — controller catches ArgumentException and returns 404
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Download_SetsContentDispositionHeader_WhenFileExists()
    {
        // Arrange — write a real temporary JPEG file so PhysicalFile succeeds.
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.jpg");
        await File.WriteAllBytesAsync(tempFile, new byte[] { 0xFF, 0xD8, 0xFF });

        try
        {
            var db = Substitute.For<IDbService>();
            db.GetAsync<RepairOrderPhoto>(Arg.Any<string>(), Arg.Any<object>())
                .Returns(new RepairOrderPhoto
                {
                    Id = 5,
                    RepairOrderId = 1,
                    FilePath = "/uploads/images_tenant_user/orders/1/test.jpg",
                    FileName = "test.jpg",
                });

            var fileStorage = Substitute.For<IFileStorageService>();
            fileStorage.GetFullPath(Arg.Any<string>()).Returns(tempFile);

            var tenantCtx = Substitute.For<ITenantContext>();

            var controller = BuildController(db, fileStorage, tenantCtx);

            // Set up a real HttpContext so we can inspect response headers.
            var httpCtx = new DefaultHttpContext();
            controller.ControllerContext.HttpContext = httpCtx;
            // Replace the default user with an authenticated one.
            httpCtx.User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, "test-user"),
            ], "TestAuth"));

            // Act
            var result = await controller.Download(5);

            // Assert — result is a file and the header was written
            var fileResult = Assert.IsType<PhysicalFileResult>(result);
            Assert.Equal("image/jpeg", fileResult.ContentType);
            Assert.True(
                httpCtx.Response.Headers.ContainsKey("Content-Disposition"),
                "Content-Disposition header should be present so browsers/share sheet can name the file.");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Upload — tenant+user-scoped subfolder
// ─────────────────────────────────────────────────────────────────────────────
public class TenantUserPhotoStorageTests
{
    private static RepairOrderPhotoController BuildAuthenticatedController(
        IDbService db,
        IFileStorageService fileStorage,
        ITenantContext tenantContext,
        string username,
        string tenantSlug)
    {
        // Wire up a tenant
        tenantContext.CurrentTenant.Returns(new Tenant { Slug = tenantSlug });

        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Role, "Owner"),
        ], "TestAuth"));

        var controller = new RepairOrderPhotoController(db, fileStorage, tenantContext);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user },
        };

        return controller;
    }

    [Theory]
    [InlineData("joes-taller", "admin", "images_joes-taller_admin/orders/7")]
    [InlineData("My Tenant", "John Doe", "images_My_Tenant_John_Doe/orders/7")]
    [InlineData("../evil", "bad/../u", "images____evil_bad____u/orders/7")]
    public async Task Upload_PassesTenantUserScopedSubfolder(
        string slug, string username, string expectedSubFolder)
    {
        // Arrange
        const int repairOrderId = 7;

        var db = Substitute.For<IDbService>();
        db.GetAsync<RepairOrderPhoto>(Arg.Any<string>(), Arg.Any<object>())
            .Returns(new RepairOrderPhoto
            {
                Id = 1,
                RepairOrderId = repairOrderId,
                FileName = "img.jpg",
                FilePath = $"/uploads/{expectedSubFolder}/img.jpg",
            });

        var fileStorage = Substitute.For<IFileStorageService>();
        fileStorage
            .SaveFileAsync(
                Arg.Any<IFormFile>(),
                Arg.Any<string>(),
                Arg.Any<string[]>(),
                Arg.Any<long>())
            .Returns($"/uploads/{expectedSubFolder}/img.jpg");

        var tenantCtx = Substitute.For<ITenantContext>();

        var sut = BuildAuthenticatedController(db, fileStorage, tenantCtx, username, slug);

        // Build a minimal IFormFile that passes the extension + size guards
        var fileContent = new byte[] { 0xFF, 0xD8, 0xFF }; // JPEG magic bytes
        var formFile = Substitute.For<IFormFile>();
        formFile.FileName.Returns("img.jpg");
        formFile.Length.Returns(fileContent.Length);
        formFile.ContentType.Returns("image/jpeg");
        formFile.CopyToAsync(Arg.Any<Stream>()).Returns(Task.CompletedTask);

        // Act
        var result = await sut.Upload(repairOrderId, [formFile], description: null);

        // Assert — SaveFileAsync was called with the correct tenant+user subfolder
        await fileStorage.Received(1).SaveFileAsync(
            Arg.Any<IFormFile>(),
            Arg.Is<string>(s => s == expectedSubFolder),
            Arg.Any<string[]>(),
            Arg.Any<long>());

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Upload_FallsBackToSharedFolder_WhenTenantNotResolved()
    {
        // Arrange — no tenant in context, no username claim
        var db = Substitute.For<IDbService>();
        db.GetAsync<RepairOrderPhoto>(Arg.Any<string>(), Arg.Any<object>())
            .Returns(new RepairOrderPhoto { Id = 1, RepairOrderId = 1, FileName = "img.jpg", FilePath = "/uploads/images_shared_unknown/orders/1/img.jpg" });

        var fileStorage = Substitute.For<IFileStorageService>();
        fileStorage
            .SaveFileAsync(Arg.Any<IFormFile>(), Arg.Any<string>(), Arg.Any<string[]>(), Arg.Any<long>())
            .Returns("/uploads/images_shared_unknown/orders/1/img.jpg");

        var tenantCtx = Substitute.For<ITenantContext>();
        tenantCtx.CurrentTenant.Returns((Tenant?)null);

        // No ClaimTypes.Name claim
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Role, "Owner"),
        ], "TestAuth"));

        var controller = new RepairOrderPhotoController(db, fileStorage, tenantCtx);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user },
        };

        var formFile = Substitute.For<IFormFile>();
        formFile.FileName.Returns("img.jpg");
        formFile.Length.Returns(100L);
        formFile.ContentType.Returns("image/jpeg");
        formFile.CopyToAsync(Arg.Any<Stream>()).Returns(Task.CompletedTask);

        // Act
        var result = await controller.Upload(1, [formFile], null);

        // Assert — falls back to "shared" tenant and "unknown" user
        await fileStorage.Received(1).SaveFileAsync(
            Arg.Any<IFormFile>(),
            Arg.Is<string>(s => s.StartsWith("images_shared_unknown/")),
            Arg.Any<string[]>(),
            Arg.Any<long>());
    }
}
