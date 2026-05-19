using EatTogether.Models.DTOs;
using EatTogether.Models.Infra;
using EatTogether.Models.Services;
using EatTogether.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EatTogether.Controllers
{
	[RequirePermission("Menu_Manage")]
	public class CategoriesController : Controller
	{
		private readonly CategoryService _categoryService;
		private readonly DishService _dishService;

		public CategoriesController(CategoryService categoryService, DishService dishService)
		{
			_categoryService = categoryService;
			_dishService = dishService;
		}

		// GET: Categories
		public async Task<IActionResult> Index()
		{
			var dtos = await _categoryService.GetAllAsync();
			var vms = dtos.Select(d => d.ToViewModel()).ToList();

			// 取得所有餐點以供詳情顯示
			var allDishes = await _dishService.GetAllAsync();

			ViewBag.CategoriesJson = System.Text.Json.JsonSerializer.Serialize(
				vms.Select(vm => new {
					id = vm.Id,
					categoryName = vm.CategoryName,
					imageUrl = vm.ImageUrl,
					parentCategoryName = vm.ParentCategoryName,
					dishCount = vm.DishCount,
					dishes = allDishes.Where(d => d.CategoryId == vm.Id).Select(d => new {
						dishName = d.DishName,
						price = d.Price,
						isActive = d.IsActive
					})
				})
			);

			// 準備下拉選單給 Modal 使用
			ViewBag.ParentCategoryOptions = await GetParentCategoryOptionsAsync();

			return View(vms);
		}

		// 用於 Modal 提交的新增
		[HttpPost]
		public async Task<IActionResult> Create([FromBody] CategoryViewModel vm)
		{
			try
			{
				if (!ModelState.IsValid)
				{
					return BadRequest(new { message = "資料格式不正確" });
				}

				var allCategories = await _categoryService.GetAllAsync();
                
                // 改用「目前最大值 + 1」作為預設排序，這是最穩定的做法
                if (vm.DisplayOrder <= 0)
                {
                    vm.DisplayOrder = allCategories.Any() ? allCategories.Max(c => c.DisplayOrder) + 1 : 1;
                }

				await _categoryService.CreateAsync(vm.ToDto());
				return Ok(new { message = "新增成功" });
			}
			catch (Exception ex)
			{
                // 捕捉具體的資料庫或邏輯錯誤並回傳
                var innerMsg = ex.InnerException != null ? " (" + ex.InnerException.Message + ")" : "";
				return StatusCode(500, new { message = "新增失敗: " + ex.Message + innerMsg });
			}
		}

		// 用於 Modal 提交的編輯 (如果需要)
		[HttpPost]
		public async Task<IActionResult> Edit(int id, [FromBody] CategoryViewModel vm)
		{
			if (id != vm.Id) return BadRequest(new { message = "ID 不符" });
			
			if (!ModelState.IsValid)
			{
				var errors = ModelState.ToDictionary(
					kvp => kvp.Key,
					kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray()
				);
				return BadRequest(errors);
			}

			await _categoryService.UpdateAsync(vm.ToDto());
			return Ok(new { message = "更新成功" });
		}

		// 停用分類
		[HttpPost]
		public async Task<IActionResult> Disable(int id)
		{
			await _categoryService.DisableAsync(id);
			await _categoryService.DisableDishesByCategoryAsync(id);
			return Ok(new { message = "已停用" });
		}

		[HttpPost]
		public async Task<IActionResult> BatchDisable([FromBody] BatchRequestDto request)
		{
			if (request?.Ids == null || !request.Ids.Any()) return BadRequest("無項目可操作。");
			await _categoryService.BatchDisableAsync(request.Ids);
			foreach (var id in request.Ids)
				await _categoryService.DisableDishesByCategoryAsync(id);
			return Ok();
		}

		[HttpPost]
		public async Task<IActionResult> Enable(int id)
		{
			await _categoryService.EnableAsync(id);
			await _categoryService.EnableDishesByCategoryAsync(id);
			return Ok();
		}

		[HttpPost]
		public async Task<IActionResult> BatchEnable([FromBody] BatchRequestDto request)
		{
			if (request?.Ids == null || !request.Ids.Any()) return BadRequest("無項目可操作。");
			await _categoryService.BatchEnableAsync(request.Ids);
			foreach (var id in request.Ids)
				await _categoryService.EnableDishesByCategoryAsync(id);
			return Ok();
		}

		[HttpPost]
		public async Task<IActionResult> BatchDelete([FromBody] BatchRequestDto request)
		{
			if (request?.Ids == null || !request.Ids.Any()) return BadRequest("無項目可操作。");
			await _categoryService.BatchDeleteAsync(request.Ids);
			return Ok();
		}

		[HttpGet]
		public async Task<IActionResult> Details(int id)
		{
			var dishes = await _categoryService.GetDishesByCategoryAsync(id);
			return Json(dishes.Select(d => new {
				id       = d.Id,
				dishName = d.DishName,
				price    = d.Price,
				isActive = d.IsActive
			}));
		}

		[HttpPost]
		public async Task<IActionResult> UpdateOrder([FromBody] OrderedIdsDto request)
		{
			if (request?.OrderedIds == null || !request.OrderedIds.Any()) return BadRequest("無順序資料。");
			await _categoryService.UpdateOrderAsync(request.OrderedIds);
			return Ok();
		}

		private async Task<List<SelectListItem>> GetParentCategoryOptionsAsync(int excludeId = 0)
		{
			var allCategories = await _categoryService.GetAllAsync();

			var options = allCategories
				.Where(c => c.Id != excludeId)
				.Select(c => new SelectListItem
				{
					Value = c.Id.ToString(),
					Text = c.CategoryName
				})
				.ToList();

			options.Insert(0, new SelectListItem { Value = "", Text = "（無，設為頂層分類）" }); 

			return options;
		}

		// ── 圖片上傳 ──────────────────────────────────────
		// 接收前端裁切後的 base64，存到 wwwroot/images/categories/
		// 回傳可直接使用的相對路徑 /images/categories/xxx.jpg
		[HttpPost]
		public async Task<IActionResult> UploadImage([FromBody] CategoryImageUploadRequest request)
		{
			try
			{
				if (string.IsNullOrEmpty(request?.Base64Data))
					return BadRequest("未提供圖片資料");

				var imageUrl = await ImageHelper.SaveBase64ImageAsync(request.Base64Data, request.CategoryName, "categories");
				return Ok(new { imageUrl });
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { message = "圖片處理失敗: " + ex.Message });
			}
		}
	}
}
