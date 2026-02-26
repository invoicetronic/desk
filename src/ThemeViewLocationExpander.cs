using Microsoft.AspNetCore.Mvc.Razor;

namespace Desk;

public class ThemeViewLocationExpander : IViewLocationExpander
{
    public void PopulateValues(ViewLocationExpanderContext context)
    {
    }

    public IEnumerable<string> ExpandViewLocations(
        ViewLocationExpanderContext context, IEnumerable<string> viewLocations)
    {
        // Prepend custom/views/ locations for ISV override
        var customLocations = viewLocations.Select(loc =>
            loc.Replace("/Pages/", "/custom/views/"));

        return customLocations.Concat(viewLocations);
    }
}
