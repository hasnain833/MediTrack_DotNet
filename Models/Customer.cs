namespace MediTrack.Models
{
    public class Customer
    {
        public int Id { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Email { get; set; }
    }
}
