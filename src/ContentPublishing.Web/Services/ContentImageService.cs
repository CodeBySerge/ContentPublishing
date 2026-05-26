using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Hosting;
using ContentPublishing.Web.Models;

namespace ContentPublishing.Web.Services
{
    public class ContentImageService
    {
        private readonly ApplicationDbContext _db;

        public ContentImageService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<ContentImageEntity> SavePrimaryImageAsync(Guid contentId, HttpPostedFileBase file, int? cropX, int? cropY, int? cropWidth, int? cropHeight)
        {
            if (file == null || file.ContentLength <= 0)
            {
                return null;
            }

            var uploadsRoot = HostingEnvironment.MapPath("~/Content/Uploads/ContentImages");
            Directory.CreateDirectory(uploadsRoot);

            var extension = Path.GetExtension(file.FileName);
            var fileName = $"{contentId:N}_{Guid.NewGuid():N}{extension}";
            var absolutePath = Path.Combine(uploadsRoot, fileName);
            var relativePath = $"/Content/Uploads/ContentImages/{fileName}";

            using (var originalImage = Image.FromStream(file.InputStream))
            {
                var applyCrop = cropWidth.HasValue && cropHeight.HasValue && cropWidth.Value > 0 && cropHeight.Value > 0;
                if (applyCrop)
                {
                    var cropRectangle = NormalizeCropRectangle(originalImage.Width, originalImage.Height, cropX ?? 0, cropY ?? 0, cropWidth.Value, cropHeight.Value);
                    using (var croppedBitmap = new Bitmap(cropRectangle.Width, cropRectangle.Height))
                    using (var graphics = Graphics.FromImage(croppedBitmap))
                    {
                        graphics.DrawImage(originalImage, new Rectangle(0, 0, croppedBitmap.Width, croppedBitmap.Height), cropRectangle, GraphicsUnit.Pixel);
                        SaveBitmap(croppedBitmap, absolutePath, extension);
                    }
                }
                else
                {
                    originalImage.Save(absolutePath);
                }
            }

            foreach (var existing in _db.ContentImages.Where(i => i.ContentId == contentId && i.IsPrimary))
            {
                existing.IsPrimary = false;
            }

            var entity = new ContentImageEntity
            {
                ImageId = Guid.NewGuid(),
                ContentId = contentId,
                FileName = fileName,
                RelativePath = relativePath,
                ContentType = file.ContentType,
                CropX = cropX,
                CropY = cropY,
                CropWidth = cropWidth,
                CropHeight = cropHeight,
                IsPrimary = true,
                CreatedDate = DateTime.UtcNow
            };

            _db.ContentImages.Add(entity);
            await _db.SaveChangesAsync();
            return entity;
        }

        private static Rectangle NormalizeCropRectangle(int imageWidth, int imageHeight, int x, int y, int width, int height)
        {
            var safeX = Math.Max(0, Math.Min(x, imageWidth - 1));
            var safeY = Math.Max(0, Math.Min(y, imageHeight - 1));
            var safeWidth = Math.Max(1, Math.Min(width, imageWidth - safeX));
            var safeHeight = Math.Max(1, Math.Min(height, imageHeight - safeY));
            return new Rectangle(safeX, safeY, safeWidth, safeHeight);
        }

        private static void SaveBitmap(Bitmap bitmap, string absolutePath, string extension)
        {
            var format = ImageFormat.Png;
            if (string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase) || string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase))
            {
                format = ImageFormat.Jpeg;
            }
            else if (string.Equals(extension, ".gif", StringComparison.OrdinalIgnoreCase))
            {
                format = ImageFormat.Gif;
            }

            bitmap.Save(absolutePath, format);
        }
    }
}
