namespace eInvWorld.Models.JsonModels
{
    public class Contact
    {
        public List<Telephone> Telephone { get; set; } = new();
        public List<ElectronicMail> ElectronicMail { get; set; } = new();
    }
}
