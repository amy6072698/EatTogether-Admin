namespace EatTogether.Models.Infra
{
	public class ImageHelper
	{
		// 儲存裁切後的 base64 圖片到指定子資料夾
		// subFolder 傳入 "dishes" / "setmeals" / "categories"
		public static async Task<string> SaveBase64ImageAsync(string base64Data, string fileNamePrefix, string subFolder)
		{
			if (string.IsNullOrEmpty(base64Data)) return null;

			// 過濾 Data URI 標頭
			var base64 = base64Data.Contains(",")
				? base64Data.Split(",")[1]
				: base64Data;

			// 將 Base64 字串轉換為 byte array
			var bytes = Convert.FromBase64String(base64);

			// 產生安全的檔名
			var safeName = fileNamePrefix ?? "";
			foreach(char c in Path.GetInvalidFileNameChars())
			{
				safeName = safeName.Replace(c, '_');
			}

			var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", subFolder);
			if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

			// 刪除同名的所有可能舊檔
			foreach (var ext in new[] { ".jpg", ".jpeg", ".png" })
			{
				var oldFile = Path.Combine(folderPath, $"{safeName}{ext}");
				if (File.Exists(oldFile)) File.Delete(oldFile);
			}

			var fileName = $"{safeName}.jpg";

			// 寫入硬碟
			await File.WriteAllBytesAsync(Path.Combine(folderPath, fileName), bytes);

			// 只回傳檔名，不含路徑前綴
			return fileName;
		}

		// 回傳含路徑前綴的相對 URL，例如 /images/dishes/BBQ豬肋排.jpg
		public static string ResolveImageUrl(string storedFileName, string itemName, string subFolder)
		{
			if (!string.IsNullOrEmpty(storedFileName)) return $"/images/{subFolder}/{storedFileName}";

			var safeName = itemName ?? "";
			foreach(char c in Path.GetInvalidFileNameChars())
			{
				safeName = safeName.Replace(c, '_');
			}

			var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", subFolder);

			var match = Directory.Exists(folderPath)
				? Directory.EnumerateFiles(folderPath, $"{safeName}.*").FirstOrDefault()
				: null;

			return match != null
				? $"/images/{subFolder}/{Path.GetFileName(match)}"
				: null;
		}

		// 保留原始副檔名、Guid 檔名
		public static async Task<string> SaveFormFileImageAsync(IFormFile file, string subFolder)
		{
			if(file == null || file.Length == 0) return null;

			var supportedTypes = new[] { ".jpg", ".jpeg", ".png", ".webp" };
			var fileExt = Path.GetExtension(file.FileName).ToLower();

			if (!supportedTypes.Contains(fileExt))
				throw new InvalidOperationException("僅支援 JPG, PNG, WEBP 格式圖片");

			var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", subFolder);
			if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

			var fileName = Guid.NewGuid().ToString("N")[..12] + fileExt;
			var filePath = Path.Combine(folderPath, fileName);

			using var stream = new FileStream(filePath, FileMode.Create);
			await file.CopyToAsync(stream);

			return fileName;
		}
	}
}
