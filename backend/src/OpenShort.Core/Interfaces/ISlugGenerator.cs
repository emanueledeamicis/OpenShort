namespace OpenShort.Core.Interfaces;

public interface ISlugGenerator
{
    string GenerateSlug(int length = 7);
}
