using System.Collections.Generic;

namespace eInvWorld.Models.InputModel
{
    public class IdTypes
    {
        public string Code { get; set; } // Code for the identification type
        public string Description { get; set; } // Description of the identification type

        // Constructor
        public IdTypes(string code, string description)
        {
            Code = code;
            Description = description;
        }

        // Static method to get predefined list of IdType
        public static List<IdTypes> GetIdTypes()
        {
            return new List<IdTypes>
            {
                new IdTypes("NRIC", "Identification Card No."),
                new IdTypes("PASSPORT", "Passport No."),
                new IdTypes("BRN", "Business Registration No."),
                new IdTypes("ARMY", "Army No.")
            };
        }
    }
}
