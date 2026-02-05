using OpenShort.Core.Entities;

namespace OpenShort.Core.Interfaces;

public interface ILinkService
{
    Task<IEnumerable<Link>> GetAllAsync();
    Task<Link?> GetByIdAsync(long id);
    Task<Link?> GetBySlugAsync(string slug);
    Task<Link?> CreateAsync(Link link);
    Task<bool> UpdateAsync(Link link);
    Task<bool> DeleteAsync(long id);
    Task<bool> ExistsAsync(long id);
}
