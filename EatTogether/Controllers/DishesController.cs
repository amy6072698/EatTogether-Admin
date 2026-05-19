using EatTogether.Models.DTOs;
using EatTogether.Models.Infra;
using EatTogether.Models.Services;
using EatTogether.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EatTogether.Controllers
{
	[Route("[controller]")]
	public class DishesController : Controller
    {
        private readonly DishService _dishService;
        private readonly CategoryService _categoryService;

        public DishesController(DishService dishService, CategoryService categoryService)
        {
            _dishService     = dishService;
            _categoryService = categoryService;
        }

		[HttpGet("Index")]
		public async Task<IActionResult> Index(bool newDish = false)
		{
            var dtos = await _dishService.GetAllAsync();
            var vms = dtos.Select(d => {
                var vm = d.ToViewModel();
                vm.ImageUrl = ImageHelper.ResolveImageUrl(vm.ImageUrl, vm.DishName, "dishes");
                return vm;
            }).ToList();
            return View(vms);
        }

        [HttpGet("Create")]
        public async Task<IActionResult> Create()
        {
            var allDishes = await _dishService.GetAllAsync();
            int nextOrder = allDishes.Any() ? allDishes.Max(d => d.DisplayOrder) + 1 : 1;
            var vm = new DishViewModel { DisplayOrder = nextOrder };
            vm.CategoryOptions = await GetCategoryOptionsAsync();
            return View(vm);
        }

        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromForm] DishViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                vm.CategoryOptions = await GetCategoryOptionsAsync();
                return View(vm);
            }

            if (!string.IsNullOrEmpty(vm.CroppedImageData))
            {
                // 新增時，直接用餐點名稱命名
                vm.ImageUrl = await ImageHelper.SaveBase64ImageAsync(vm.CroppedImageData, vm.DishName, "dishes");
            }

            await _dishService.CreateAsync(vm.ToDto());
			return RedirectToAction(nameof(Index), new { newDish = true });
		}

        [HttpGet("Edit/{id}")]
        public async Task<IActionResult> Edit(int id)
        {
            var dto = await _dishService.GetByIdAsync(id);
            if (dto == null) return NotFound();
            var vm = dto.ToViewModel();
            vm.CategoryOptions = await GetCategoryOptionsAsync();

			vm.ImageUrl = ImageHelper.ResolveImageUrl(vm.ImageUrl, vm.DishName, "dishes");
			return View(vm);
        }

        [HttpPost("Edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [FromForm] DishViewModel vm)
        {
            if (id != vm.Id) return BadRequest();

            if (!ModelState.IsValid)
            {
                vm.CategoryOptions = await GetCategoryOptionsAsync();
                return View(vm);
            }

            if (!string.IsNullOrEmpty(vm.CroppedImageData))
            {
                var oldDto = await _dishService.GetByIdAsync(id);
                var oldFileName = oldDto?.ImageUrl;

                vm.ImageUrl = await ImageHelper.SaveBase64ImageAsync(vm.CroppedImageData, vm.DishName, "dishes");

				// 若餐點改名，舊檔名與新檔名不同，刪除舊檔避免殘留
				if (!string.IsNullOrEmpty(oldFileName) && oldFileName != vm.ImageUrl)
				{
                    var oldFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "dishes", oldFileName);
                    if (System.IO.File.Exists(oldFilePath))
                    {
                        System.IO.File.Delete(oldFilePath);
					}
				}
			}

            await _dishService.UpdateAsync(vm.ToDto());
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("Disable/{id}")]
        public async Task<IActionResult> Disable(int id)
        {
            await _dishService.DisableAsync(id);
            return Ok();
        }

        [HttpPost("BatchDisable")]
        public async Task<IActionResult> BatchDisable([FromBody] BatchRequestDto request)
        {
            if (request?.Ids == null || !request.Ids.Any()) return BadRequest("無項目可操作。");
            await _dishService.BatchDisableAsync(request.Ids);
            return Ok();
        }

        [HttpPost("Enable/{id}")]
        public async Task<IActionResult> Enable(int id)
        {
            await _dishService.EnableAsync(id);
            return Ok();
        }

        [HttpPost("BatchEnable")]
        public async Task<IActionResult> BatchEnable([FromBody] BatchRequestDto request)
        {
            if (request?.Ids == null || !request.Ids.Any()) return BadRequest("無項目可操作。");
            await _dishService.BatchEnableAsync(request.Ids);
            return Ok();
        }

        [HttpPost("BatchDelete")]
        public async Task<IActionResult> BatchDelete([FromBody] BatchRequestDto request)
        {
            if (request?.Ids == null || !request.Ids.Any()) return BadRequest("無項目可操作。");
            await _dishService.BatchDeleteAsync(request.Ids);
            return Ok();
        }

        [HttpPost("UpdateOrder")]
        public async Task<IActionResult> UpdateOrder([FromBody] OrderedIdsDto request)
        {
            if (request?.OrderedIds == null || !request.OrderedIds.Any()) return BadRequest("無順序可更新。");
            await _dishService.UpdateOrderAsync(request.OrderedIds);
            return Ok();
        }

        [HttpPost("ToggleActive/{id}")]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var dish = await _dishService.GetByIdAsync(id);
            if (dish == null) return NotFound();
            dish.IsActive = !dish.IsActive;
            await _dishService.UpdateAsync(dish);
            return Ok(new { isActive = dish.IsActive });
        }

		[HttpGet("GetAllJson")]
		[AllowAnonymous]
		public async Task<IActionResult> GetAllJson()
        {
            var dtos = await _dishService.GetAllAsync();
            return Json(dtos.Select(d => new
            {
                id = d.Id,
                dishName = d.DishName,
                price = d.Price,
                categoryId = d.CategoryId, 
                imageUrl = d.ImageUrl       //補上圖片路徑，前台才好顯示}));     
            }));
        }
		[HttpGet("GetActiveJson")]
		[HttpGet("active")]
		[AllowAnonymous]
		public async Task<IActionResult> GetActiveJson()
		{
			var dtos = await _dishService.GetAllActiveAsync();

			return Json(dtos.Select(d => {
				string imageUrl = ImageHelper.ResolveImageUrl(d.ImageUrl, d.DishName, "dishes");

				return new
				{
					id = d.Id,
					dishName = d.DishName,
					description = d.Description,
					price = d.Price,
					categoryId = d.CategoryId,
					categoryName = d.CategoryName,
					imageUrl = imageUrl,  // ← 用補過的
					isRecommended = d.IsRecommended,
					isPopular = d.IsPopular,
					isVegetarian = d.IsVegetarian,
					spicyLevel = d.SpicyLevel,
					ingredientsJson = d.IngredientsJson,
					isLimited = d.IsLimited,
					startDate = d.StartDate,
					endDate = d.EndDate,
					averageScore = d.AverageScore,
					ratingCount = d.RatingCount,
					stockStatus = d.StockStatus
				};
			}));
		}

		[HttpGet("GetByIdJson")]
		[AllowAnonymous]
		public async Task<IActionResult> GetByIdJson(int id)
		{
			var dto = await _dishService.GetByIdAsync(id);
			if (dto == null) return NotFound();
			return Json(new
			{
				id = dto.Id,
				dishName = dto.DishName,
				description = dto.Description,
				price = dto.Price,
				categoryId = dto.CategoryId,
				categoryName = dto.CategoryName,
				imageUrl = dto.ImageUrl,
				isRecommended = dto.IsRecommended,
				isPopular = dto.IsPopular,
				isVegetarian = dto.IsVegetarian,
				spicyLevel = dto.SpicyLevel
			});
		}

		private async Task<List<SelectListItem>> GetCategoryOptionsAsync()
        {
            var categories = await _categoryService.GetAllAsync();
            return categories.Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.CategoryName }).ToList();
        }
    }
}
