namespace DChemist.Models
{
    public class NavigationItem
    {
        public string Title { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string PageType { get; set; } = string.Empty;
        public bool RequiresAdmin { get; set; }
    }
}
