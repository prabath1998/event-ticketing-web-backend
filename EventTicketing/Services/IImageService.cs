using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace EventTicketing.Services
{
    public interface IImageService
    {
        Task<(string FileName, string FilePath, long FileSize)> SaveImageAsync(IFormFile imageFile);
        Task<bool> DeleteImageAsync(string fileName);
        string GetImageUrl(string fileName);
    }

    public class ImageService : IImageService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<ImageService> _logger;

        public ImageService(IWebHostEnvironment environment, IHttpContextAccessor httpContextAccessor, ILogger<ImageService> logger)
        {
            _environment = environment;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task<(string FileName, string FilePath, long FileSize)> SaveImageAsync(IFormFile imageFile)
        {
            if (imageFile == null || imageFile.Length == 0)
                throw new ArgumentException("Image file is required");

            // Validate file type
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var extension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
                throw new ArgumentException("Invalid image format. Allowed: JPG, JPEG, PNG, GIF, WEBP");

            // Validate file size (max 5MB)
            if (imageFile.Length > 5 * 1024 * 1024)
                throw new ArgumentException("Image size must be less than 5MB");

            // Generate unique filename
            var fileName = $"{Guid.NewGuid()}{extension}";
            var folderPath = Path.Combine(_environment.WebRootPath, "images", "events");
            var filePath = Path.Combine(folderPath, fileName);

            // Ensure directory exists
            Directory.CreateDirectory(folderPath);

            // Save file
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(stream);
            }

            return (fileName, filePath, imageFile.Length);
        }

        public async Task<bool> DeleteImageAsync(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return false;

            var filePath = Path.Combine(_environment.WebRootPath, "images", "events", fileName);

            if (File.Exists(filePath))
            {
                await Task.Run(() => File.Delete(filePath));
                return true;
            }

            return false;
        }

        public string GetImageUrl(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return null;

            var request = _httpContextAccessor.HttpContext?.Request;
            if (request == null)
                return null;

            return $"{request.Scheme}://{request.Host}/images/events/{fileName}";
        }
    }
}