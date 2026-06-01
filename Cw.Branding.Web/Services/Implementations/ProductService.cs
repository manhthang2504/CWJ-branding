using Cw.Branding.Web.Data;
using Cw.Branding.Web.Models.Entities;
using Cw.Branding.Web.Services.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Cw.Branding.Web.Services;

public class ProductService : IProductService
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _env; // Thêm env để xử lý file vật lý

    public ProductService(AppDbContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
    }
    public async Task<bool> IsCodeExistsAsync(string code)
    {
        // Kiểm tra không phân biệt hoa thường
        return await _context.Products.AnyAsync(p => p.Code.ToLower() == code.ToLower());
    }
    // Lấy tất cả cho Admin, kèm theo thông tin quan hệ để hiển thị lên bảng
    // Services/Implementations/ProductService.cs
    public async Task<List<Product>> GetAllForAdminAsync(string? searchTerm = null, int? categoryId = null, int? brandId = null, bool? isActive = null)
    {
        var query = _context.Products
            .Include(p => p.Category)
            .Include(p => p.Brand)
            .Include(p => p.MachineType) // Giữ lại để hiện tên loại máy trên bảng
            .Include(p => p.Images)      
            .AsQueryable();

        // 1. Lọc theo từ khóa
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            searchTerm = searchTerm.ToLower().Trim();
            query = query.Where(p => p.NameVi.ToLower().Contains(searchTerm)
                                  || p.NameEn.ToLower().Contains(searchTerm)
                                  || p.Code.ToLower().Contains(searchTerm));
        }

        // 2. Lọc theo Chuyên khoa
        if (categoryId.HasValue)
            query = query.Where(p => p.CategoryId == categoryId.Value);

        // 3. Lọc theo Hãng
        if (brandId.HasValue)
            query = query.Where(p => p.BrandId == brandId.Value);

        // 4. Lọc theo Trạng thái
        if (isActive.HasValue)
            query = query.Where(p => p.IsActive == isActive.Value);

        return await query
            .OrderBy(p => p.DisplayOrder)
            .ThenByDescending(p => p.CreatedAt)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Product?> GetByIdAsync(int id)
    {
        return await _context.Products
            .Include(p => p.Images) // Load luôn ảnh
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    // Cho trang chủ (Chỉ lấy sản phẩm nổi bật & đang active)
    public async Task<List<Product>> GetFeaturedProductsAsync(int limit = 8)
    {
        return await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Images)
            .Where(p => p.IsActive && p.IsFeatured)
            .OrderBy(p => p.DisplayOrder)
            .Take(limit)
            .AsNoTracking()
            .ToListAsync();
    }

    // Cho trang danh sách theo Chuyên khoa
    public async Task<List<Product>> GetActiveByCategoryAsync(int categoryId)
    {
        return await _context.Products
            .Include(p => p.Brand)
            .Include(p => p.MachineType)
            .Include(p => p.Images)
            .Where(p => p.IsActive && p.CategoryId == categoryId)
            .OrderBy(p => p.DisplayOrder)
            .AsNoTracking()
            .ToListAsync();
    }

    // Lấy chi tiết bằng Slug (hỗ trợ cả tiếng Anh và tiếng Việt)
    public async Task<Product?> GetBySlugAsync(string slug, string lang)
    {
        var query = _context.Products
            .Include(p => p.Category)
            .Include(p => p.Brand)
            .Include(p => p.MachineType)
            .Include(p => p.Images)
            .Where(p => p.IsActive);

        if (lang.ToLower() == "vi")
        {
            return await query.FirstOrDefaultAsync(p => p.SlugVi == slug);
        }

        return await query.FirstOrDefaultAsync(p => p.SlugEn == slug);
    }

    public async Task<bool> CreateAsync(Product product)
    {
        try
        {
            product.CreatedAt = DateTime.UtcNow;
            _context.Products.Add(product);
            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception)
        {
            // Note: Thực tế nên dùng ILogger để log lỗi ở đây
            return false;
        }
    }
    // Services/Implementations/ProductService.cs
    public async Task<bool> UpdateDisplayOrderAsync(int id, int displayOrder)
    {
        var product = await _context.Products.FindAsync(id);
        if (product == null) return false;

        product.DisplayOrder = displayOrder;
        product.UpdatedAt = DateTime.UtcNow;

        _context.Products.Update(product);
        return await _context.SaveChangesAsync() > 0; // Trả về true nếu có ít nhất 1 dòng bị thay đổi
    }
    // CẬP NHẬT: Hàm Update Cũ (Giữ lại nếu anh có xài chỗ khác không liên quan đến ảnh)
    public async Task<bool> UpdateAsync(Product product)
    {
        try
        {
            var existing = await _context.Products.FindAsync(product.Id);
            if (existing == null) return false;

            existing.Code = product.Code;
            existing.NameVi = product.NameVi;
            existing.NameEn = product.NameEn;
            existing.ShortDescriptionVi = product.ShortDescriptionVi;
            existing.ShortDescriptionEn = product.ShortDescriptionEn;
            existing.DescriptionVi = product.DescriptionVi;
            existing.DescriptionEn = product.DescriptionEn;
            existing.TechnicalSpecsVi = product.TechnicalSpecsVi;
            existing.TechnicalSpecsEn = product.TechnicalSpecsEn;
            existing.SlugVi = product.SlugVi;
            existing.SlugEn = product.SlugEn;
            existing.IsFeatured = product.IsFeatured;
            existing.IsActive = product.IsActive;
            existing.DisplayOrder = product.DisplayOrder;
            existing.CategoryId = product.CategoryId;
            existing.BrandId = product.BrandId;
            existing.MachineTypeId = product.MachineTypeId;
            existing.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
    // Cập nhật signature
    public async Task<bool> CreateWithImagesAsync(Product product, List<IFormFile> newImages, string? mainImageName = null)
    {
        try
        {
            product.CreatedAt = DateTime.UtcNow;
            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            if (newImages != null && newImages.Count > 0)
            {
                var productFolder = Path.Combine(_env.WebRootPath, "uploads", "products", product.Id.ToString());
                if (!Directory.Exists(productFolder)) Directory.CreateDirectory(productFolder);

                int displayOrder = 1;
                // Biến cờ để đảm bảo ít nhất có 1 ảnh làm Main nếu mainImageName bị lỗi
                bool hasMainImage = false;

                foreach (var file in newImages)
                {
                    if (file.Length > 0)
                    {
                        var uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(file.FileName);
                        var physicalPath = Path.Combine(productFolder, uniqueFileName);

                        using (var stream = new FileStream(physicalPath, FileMode.Create))
                        {
                            await file.CopyToAsync(stream);
                        }

                        // LOGIC MỚI: Kiểm tra tên file để set IsMain
                        bool isMain = false;
                        if (!string.IsNullOrEmpty(mainImageName) && file.FileName == mainImageName && !hasMainImage)
                        {
                            isMain = true;
                            hasMainImage = true;
                        }
                        // Fallback: Nếu duyệt đến ảnh cuối mà vẫn chưa có ảnh bìa, lấy đại ảnh đầu
                        else if (string.IsNullOrEmpty(mainImageName) && displayOrder == 1)
                        {
                            isMain = true;
                            hasMainImage = true;
                        }

                        product.Images.Add(new ProductImage
                        {
                            FilePath = $"/uploads/products/{product.Id}/{uniqueFileName}",
                            IsMain = isMain,
                            DisplayOrder = displayOrder
                        });

                        displayOrder++;
                    }
                }

                // Fallback lần 2: Lỡ mainImageName gửi lên không khớp file nào thì set file đầu tiên
                if (!hasMainImage && product.Images.Any())
                {
                    product.Images.First().IsMain = true;
                }

                await _context.SaveChangesAsync();
            }

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
    // CẬP NHẬT: Xử lý Edit Product (Task 2.2.2)
    public async Task<bool> UpdateProductWithImagesAsync(Product product, List<IFormFile> newImages, List<int> deletedImageIds, int? mainImageId = null, string? mainImageName = null)
    {
        try
        {
            var existingProduct = await _context.Products.Include(p => p.Images).FirstOrDefaultAsync(p => p.Id == product.Id);
            if (existingProduct == null) return false;

            // 1. Mapping data text
            existingProduct.Code = product.Code;
            existingProduct.NameVi = product.NameVi;
            existingProduct.NameEn = product.NameEn;
            existingProduct.SlugVi = product.SlugVi;
            existingProduct.SlugEn = product.SlugEn;
            existingProduct.ShortDescriptionVi = product.ShortDescriptionVi;
            existingProduct.ShortDescriptionEn = product.ShortDescriptionEn;
            existingProduct.DescriptionVi = product.DescriptionVi;
            existingProduct.DescriptionEn = product.DescriptionEn;
            existingProduct.CategoryId = product.CategoryId;
            existingProduct.BrandId = product.BrandId;
            existingProduct.MachineTypeId = product.MachineTypeId;
            existingProduct.IsActive = product.IsActive;
            existingProduct.IsFeatured = product.IsFeatured;
            existingProduct.DisplayOrder = product.DisplayOrder;

            // 2. Xóa ảnh cũ & File vật lý
            if (deletedImageIds != null && deletedImageIds.Any())
            {
                var imagesToDelete = existingProduct.Images.Where(i => deletedImageIds.Contains(i.Id)).ToList();
                foreach (var img in imagesToDelete)
                {
                    var filePath = Path.Combine(_env.WebRootPath, img.FilePath.TrimStart('/'));
                    if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);
                    existingProduct.Images.Remove(img);
                    _context.ProductImages.Remove(img);
                }
            }

            // 3. Logic: Đang chọn ảnh cũ hay ảnh mới làm bìa?
            bool isNewImageMain = !string.IsNullOrEmpty(mainImageName);
            bool hasMainImage = false;

            // 4. Duyệt danh sách ảnh cũ để set bìa
            foreach (var img in existingProduct.Images)
            {
                // Nếu KHÔNG chọn ảnh mới làm bìa, VÀ id trùng khớp thì nó là bìa
                if (!isNewImageMain && mainImageId.HasValue && img.Id == mainImageId.Value)
                {
                    img.IsMain = true;
                    hasMainImage = true;
                }
                else
                {
                    img.IsMain = false;
                }
            }

            // 5. Thêm ảnh mới
            if (newImages != null && newImages.Count > 0)
            {
                var productFolder = Path.Combine(_env.WebRootPath, "uploads", "products", product.Id.ToString());
                if (!Directory.Exists(productFolder)) Directory.CreateDirectory(productFolder);

                int maxOrder = existingProduct.Images.Any() ? existingProduct.Images.Max(i => i.DisplayOrder) : 0;

                foreach (var file in newImages)
                {
                    if (file.Length > 0)
                    {
                        var uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(file.FileName);
                        var physicalPath = Path.Combine(productFolder, uniqueFileName);

                        using (var stream = new FileStream(physicalPath, FileMode.Create))
                        {
                            await file.CopyToAsync(stream);
                        }

                        // Nếu chọn đúng ảnh tải lên này làm bìa
                        bool isMainForThisNewFile = false;
                        if (isNewImageMain && file.FileName == mainImageName && !hasMainImage)
                        {
                            isMainForThisNewFile = true;
                            hasMainImage = true;
                        }

                        maxOrder++;
                        existingProduct.Images.Add(new ProductImage
                        {
                            FilePath = $"/uploads/products/{product.Id}/{uniqueFileName}",
                            IsMain = isMainForThisNewFile,
                            DisplayOrder = maxOrder
                        });
                    }
                }
            }

            // 6. Fallback: Nếu không có ai làm bìa, lấy ảnh số 1
            if (!hasMainImage && existingProduct.Images.Any())
            {
                existingProduct.Images.OrderBy(i => i.DisplayOrder).First().IsMain = true;
            }

            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    // CẬP NHẬT: Logic Delete Product & Folder Purge (Task 2.2.3)
    public async Task<bool> DeleteAsync(int id)
    {
        try
        {
            // 1. Tìm sản phẩm
            var product = await _context.Products.FindAsync(id);
            if (product == null) return false;

            // 2. LOGIC FOLDER PURGE: Xóa toàn bộ thư mục /uploads/products/{id}/
            var productFolder = Path.Combine(_env.WebRootPath, "uploads", "products", id.ToString());
            if (Directory.Exists(productFolder))
            {
                // Tham số 'true' ở đây là để xóa đệ quy (xóa luôn cả các file bên trong folder)
                Directory.Delete(productFolder, true);
            }

            // 3. XÓA DATABASE (EF Core sẽ tự động Cascade Delete các record trong bảng ProductImages)
            _context.Products.Remove(product);
            await _context.SaveChangesAsync();

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
    // Thêm vào ProductService.cs
    public async Task<List<Product>> GetFilteredProductsClientAsync(string? searchTerm, int? categoryId, int? brandId, int? machineTypeId)
    {
        var query = _context.Products
            .Include(p => p.Images)
            .Include(p => p.Brand)
            .Include(p => p.Category)
            .Where(p => p.IsActive)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            searchTerm = searchTerm.ToLower().Trim();
            query = query.Where(p => p.NameVi.ToLower().Contains(searchTerm)
                                  || p.NameEn.ToLower().Contains(searchTerm)
                                  || p.Code.ToLower().Contains(searchTerm));
        }

        if (categoryId.HasValue && categoryId > 0)
            query = query.Where(p => p.CategoryId == categoryId.Value);

        if (brandId.HasValue && brandId > 0)
            query = query.Where(p => p.BrandId == brandId.Value);

        if (machineTypeId.HasValue && machineTypeId > 0)
            query = query.Where(p => p.MachineTypeId == machineTypeId.Value);

        // ĐỒNG BỘ: Sắp xếp giống hệt Admin
        return await query
            .OrderBy(p => p.DisplayOrder)
            .ThenByDescending(p => p.CreatedAt)
            .AsNoTracking()
            .ToListAsync();
    }

    // 2. Cập nhật hàm Client CÓ phân trang (Hàm này khả năng cao đang kéo data lên Partial View của ông)
    public async Task<(List<Product> Items, int TotalCount)> GetFilteredProductsPaginatedAsync(
    string? searchTerm, int? categoryId, int? brandId, int? machineTypeId, int page = 1, int pageSize = 8)
    {
        var query = _context.Products
            .Include(p => p.Images)
            .Include(p => p.Brand)
            .Include(p => p.Category)
            .Where(p => p.IsActive)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            searchTerm = searchTerm.ToLower().Trim();
            query = query.Where(p => p.NameVi.ToLower().Contains(searchTerm)
                                  || p.NameEn.ToLower().Contains(searchTerm)
                                  || p.Code.ToLower().Contains(searchTerm));
        }

        if (categoryId.HasValue && categoryId > 0)
            query = query.Where(p => p.CategoryId == categoryId.Value);
        if (brandId.HasValue && brandId > 0)
            query = query.Where(p => p.BrandId == brandId.Value);
        if (machineTypeId.HasValue && machineTypeId > 0)
            query = query.Where(p => p.MachineTypeId == machineTypeId.Value);

        int totalCount = await query.CountAsync();

        // ĐỒNG BỘ: Sửa từ OrderByDescending(p => p.Id) thành cấu trúc giống Admin bên dưới
        var items = await query
            .OrderBy(p => p.DisplayOrder)
            .ThenByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync();

        return (items, totalCount);
    }
}