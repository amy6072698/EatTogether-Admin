using EatTogether.Models.DTOs;
using EatTogether.Models.Infra;
using EatTogether.Models.Repositories;
using EatTogether.Models.Services;
using EatTogether.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EatTogether.Controllers        
{
	[Route("[controller]")]
	public class SetMealsController : Controller
    {
        private readonly SetMealService _setMealService;
        private readonly DishService _dishService;
        private readonly CategoryService _categoryService;
        private readonly ISetMealRepository _setMealRepo;

        public SetMealsController(SetMealService setMealService, DishService dishService, CategoryService categoryService, ISetMealRepository setMealRepo)
        {
            _setMealService = setMealService;
            _dishService    = dishService;
            _categoryService = categoryService;
            _setMealRepo    = setMealRepo;
        }

        [HttpGet("Index")]
        public async Task<IActionResult> Index()
        {
            var dtos = await _setMealService.GetAllAsync();
            var vms = dtos.Select(d =>
            {
                var vm = d.ToViewModel();
                vm.ImageUrl = ImageHelper.ResolveImageUrl(vm.ImageUrl, vm.SetMealName, "setmeals");
                return vm;
            }).ToList();

            ViewBag.SetMealsJson = System.Text.Json.JsonSerializer.Serialize(
                vms.Select(vm => new {
                    id = vm.Id,
                    setMealName = vm.SetMealName,
                    imageUrl = vm.ImageUrl,
                    setPrice = vm.SetPrice,
                    discountType = vm.DiscountType,
                    discountValue = vm.DiscountValue,
                    startDate = vm.StartDate,
                    endDate = vm.EndDate,
                    startTime = vm.StartTime,
                    endTime = vm.EndTime,
                    isActive = vm.IsActive,
                    isPopular = vm.IsPopular,
                    isRecommended = vm.IsRecommended,
                    displayOrder = vm.DisplayOrder,
                    items = vm.Items.Select(i => new {
                        dishId = i.DishId,
                        dishName = i.DishName,
                        dishPrice = i.DishPrice,
                        categoryName = i.CategoryName,
                        quantity = i.Quantity,
                        isOptional = i.IsOptional,
                        optionGroupNo = i.OptionGroupNo,
                        pickLimit = i.PickLimit,
                        displayOrder = i.DisplayOrder
                    })
                })
            );

            return View(vms);
        }

        [HttpGet("Create")]
        public async Task<IActionResult> Create()
        {
            var allSetMeals = await _setMealService.GetAllAsync();
            int nextOrder = allSetMeals.Any() ? allSetMeals.Max(s => s.DisplayOrder) + 1 : 1;
            
            // Prepare data for the new UI
            var vm = new SetMealViewModel { DisplayOrder = nextOrder };
            await PopulateCategoriesWithDishes(vm);
            
            return View(vm);
        }

        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromForm] SetMealViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                await PopulateCategoriesWithDishes(vm); // Repopulate if validation fails
                return View(vm);
            }

            if (!string.IsNullOrEmpty(vm.CroppedImageData))
                vm.ImageUrl = await ImageHelper.SaveBase64ImageAsync(vm.CroppedImageData, vm.SetMealName, "setmeals");


            await _setMealService.CreateAsync(vm.ToDto());
            return RedirectToAction(nameof(Index));
        }

        [HttpGet("Edit/{id}")]
        public async Task<IActionResult> Edit(int id)
        {
            var dto = await _setMealService.GetByIdAsync(id);
            if (dto == null) return NotFound();

            var vm = dto.ToViewModel();

            // Populate CategoriesWithDishes for the new UI
            await PopulateCategoriesWithDishes(vm);

            ViewBag.CategoriesWithDishesJson = System.Text.Json.JsonSerializer.Serialize(
                vm.CategoriesWithDishes.Select(c => new {
                    categoryId = c.CategoryId,
                    categoryName = c.CategoryName,
                    isCategoryOptional = c.IsCategoryOptional,
                    optionGroupNoForCategory = c.OptionGroupNoForCategory,
                    pickLimitForCategory = c.PickLimitForCategory,
                    dishesInThisCategory = c.DishesInThisCategory.Select(d => new {
                        value = d.Value,
                        text = d.Text
                    }),
                    selectedItemsForCategory = c.SelectedItemsForCategory.Select(i => new {
                        dishId = i.DishId,
                        dishName = i.DishName,
                        dishPrice = i.DishPrice,
                        quantity = i.Quantity,
                        displayOrder = i.DisplayOrder
                    })
                })
            );

			vm.ImageUrl = ImageHelper.ResolveImageUrl(vm.ImageUrl, vm.SetMealName, "setmeals");

			return View(vm);
        }

        [HttpPost("Edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [FromForm] SetMealViewModel vm, [FromForm] string itemsJson)
        {
            if (id != vm.Id) return BadRequest();
            
            // 解析前端傳來的餐點資料，確保儲存基本資料時不會清空明細
            if (!string.IsNullOrEmpty(itemsJson))
            {
                try {
                    var items = System.Text.Json.JsonSerializer.Deserialize<List<SetMealItemViewModel>>(itemsJson);
                    if (items != null) vm.Items = items;
                } catch { /* 忽略解析錯誤 */ }
            }

            if (!ModelState.IsValid)
            {
                await PopulateCategoriesWithDishes(vm); // Repopulate if validation fails
                return View(vm);
            }

            if (!string.IsNullOrEmpty(vm.CroppedImageData))
            {
                // 強制覆蓋原有檔案，並用餐點名稱命名
                vm.ImageUrl = await ImageHelper.SaveBase64ImageAsync(vm.CroppedImageData, vm.SetMealName, "setmeals");
            }

            await _setMealService.UpdateAsync(vm.ToDto());
            return RedirectToAction(nameof(Index));
        }
        
        // Helper method to populate CategoriesWithDishes
        private async Task PopulateCategoriesWithDishes(SetMealViewModel vm)
        {
            var allCategories = await _categoryService.GetAllAsync();
            // 抓取「所有」餐點，確保已在套餐中的餐點即使下架了也能顯示
            var allDishes = await _dishService.GetAllAsync(); 

            var categoriesWithDishes = new List<CategoryWithDishesViewModel>();

            foreach (var category in allCategories.OrderBy(c => c.DisplayOrder))
            {
                var categoryVm = new CategoryWithDishesViewModel
                {
                    CategoryId = category.Id,
                    CategoryName = category.CategoryName,
                    DishesInThisCategory = allDishes
                        .Where(d => d.CategoryId == category.Id && (d.IsActive || vm.Items.Any(item => item.DishId == d.Id)))
                        .Select(d => new SelectListItem
                        {
                            Value = d.Id.ToString(),
                            Text = $"{(d.IsActive ? "" : "[已下架] ")}{d.DishName} (${d.Price})",
                            Selected = vm.Items.Any(item => item.DishId == d.Id)
                        }).ToList()
                };

                // Populate SelectedItemsForCategory for rendering existing items
                // 這裡必須確保所有套餐內的項目都被加入，不論是否 Active
                categoryVm.SelectedItemsForCategory = vm.Items
                    .Where(item => allDishes.Any(d => d.Id == item.DishId && d.CategoryId == category.Id))
                    .ToList();
                
                // 補足 SelectedItemsForCategory 中遺失的名稱與價格資訊 (因為 ToViewModel 時可能只有 ID)
                foreach(var item in categoryVm.SelectedItemsForCategory)
                {
                    var dish = allDishes.FirstOrDefault(d => d.Id == item.DishId);
                    if (dish != null)
                    {
                        item.DishName = dish.DishName;
                        item.DishPrice = dish.Price;
                        item.CategoryName = category.CategoryName;
                    }
                }

                var firstOptionalItem = categoryVm.SelectedItemsForCategory.FirstOrDefault(i => i.IsOptional);
                if (firstOptionalItem != null)
                {
                    categoryVm.IsCategoryOptional = true;
                    categoryVm.OptionGroupNoForCategory = firstOptionalItem.OptionGroupNo;
                    categoryVm.PickLimitForCategory = firstOptionalItem.PickLimit;
                }

                categoriesWithDishes.Add(categoryVm);
            }
            vm.CategoriesWithDishes = categoriesWithDishes;
        }

        [HttpPost("Disable/{id}")]
        public async Task<IActionResult> Disable(int id)
        {
            await _setMealService.DisableAsync(id);
            return Ok();
        }

        [HttpPost("BatchDisable")]
        public async Task<IActionResult> BatchDisable([FromBody] BatchRequestDto request)
        {
            if (request?.Ids == null || !request.Ids.Any()) return BadRequest("無項目可操作。");
            await _setMealService.BatchDisableAsync(request.Ids);
            return Ok();
        }

        [HttpPost("Enable/{id}")]
        public async Task<IActionResult> Enable(int id)
        {
            await _setMealService.EnableAsync(id);
            return Ok();
        }

        [HttpPost("BatchEnable")]
        public async Task<IActionResult> BatchEnable([FromBody] BatchRequestDto request)
        {
            if (request?.Ids == null || !request.Ids.Any()) return BadRequest("無項目可操作。");
            await _setMealService.BatchEnableAsync(request.Ids);
            return Ok();
        }

        [HttpPost("BatchDelete")]
        public async Task<IActionResult> BatchDelete([FromBody] BatchRequestDto request)
        {
            if (request?.Ids == null || !request.Ids.Any()) return BadRequest("無項目可操作。");
            await _setMealService.BatchDeleteAsync(request.Ids);
            return Ok();
        }

        [HttpPost("Clone/{id}")]
        public async Task<IActionResult> Clone(int id)
        {
            var newId = await _setMealRepo.CloneSetMealAsync(id);
            if (newId == 0) return NotFound();
            return Ok(new { newId });
        }

        [HttpPost("UpdateItems/{setMealId}")]
        public async Task<IActionResult> UpdateItems(int setMealId, [FromBody] List<SetMealItemViewModel> items)
        {
            if (items == null) return BadRequest("無項目可更新。");

            try
            {
                var dtos = items.Select(i => i.ToItemDto());
                await _setMealService.UpdateItemsAsync(setMealId, dtos);
                return Ok(new { message = "套餐內容更新成功！" });
            }
            catch (Exception ex)
            {
                // Log the exception
                return BadRequest(new { message = "更新失敗：" + ex.Message });
            }
        }

        [HttpGet("GetActiveJson")]
        [AllowAnonymous]
        public async Task<IActionResult> GetActiveJson()
        {
            var dtos = await _setMealService.GetAllActiveAsync();

            return Json(dtos.Select(d => {
                string imageUrl = ImageHelper.ResolveImageUrl(d.ImageUrl, d.SetMealName, "setmeals");
                return new {
                    id = d.Id,
                    setMealName = d.SetMealName,
                    description = d.Description,
                    setPrice = d.SetPrice,
                    imageUrl = imageUrl,
                    isRecommended = d.IsRecommended,
                    isPopular = d.IsPopular,
                    startDate = d.StartDate,
                    endDate = d.EndDate,
                    startTime = d.StartTime,
                    endTime = d.EndTime,
                    items = d.Items.Select(i => new {
                        dishId = i.DishId,
                        dishName = i.DishName,
                        dishPrice = i.DishPrice,
                        categoryName = i.CategoryName,
                        quantity = i.Quantity,
                        isOptional = i.IsOptional,
                        optionGroupNo = i.OptionGroupNo,
                        pickLimit = i.PickLimit
                    })
                };
            }));
        }

        [HttpPost("UpdateOrder")]
        public async Task<IActionResult> UpdateOrder([FromBody] OrderedIdsDto dto)
        {
            if (dto?.OrderedIds == null || !dto.OrderedIds.Any())
            {
                return BadRequest("No IDs provided for reordering.");
            }

            try
            {
                await _setMealService.UpdateOrderAsync(dto.OrderedIds);
                return Ok(new { message = "Order updated successfully." });
            }
            catch (Exception ex)
            {
                // In a real app, log this exception
                return StatusCode(500, "An error occurred while updating the order.");
            }
        }
    }
}
