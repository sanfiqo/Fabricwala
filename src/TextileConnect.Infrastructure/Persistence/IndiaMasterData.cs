namespace TextileConnect.Infrastructure.Persistence;

public static class IndiaMasterData
{
    public static readonly (string Name, string Code, string[] Cities)[] States =
    [
        ("Andhra Pradesh", "AP", ["Visakhapatnam", "Vijayawada", "Tirupati"]),
        ("Arunachal Pradesh", "AR", ["Itanagar"]),
        ("Assam", "AS", ["Guwahati", "Dibrugarh"]),
        ("Bihar", "BR", ["Patna", "Gaya", "Muzaffarpur"]),
        ("Chhattisgarh", "CG", ["Raipur", "Bhilai"]),
        ("Goa", "GA", ["Panaji", "Margao"]),
        ("Gujarat", "GJ", ["Ahmedabad", "Surat", "Vadodara", "Rajkot"]),
        ("Haryana", "HR", ["Gurugram", "Faridabad", "Panipat", "Hisar"]),
        ("Himachal Pradesh", "HP", ["Shimla", "Dharamshala", "Baddi"]),
        ("Jharkhand", "JH", ["Ranchi", "Jamshedpur", "Dhanbad"]),
        ("Karnataka", "KA", ["Bengaluru", "Mysuru", "Hubballi", "Mangaluru"]),
        ("Kerala", "KL", ["Thiruvananthapuram", "Kochi", "Kozhikode"]),
        ("Madhya Pradesh", "MP", ["Bhopal", "Indore", "Gwalior", "Jabalpur"]),
        ("Maharashtra", "MH", ["Mumbai", "Pune", "Nagpur", "Nashik"]),
        ("Manipur", "MN", ["Imphal"]),
        ("Meghalaya", "ML", ["Shillong"]),
        ("Mizoram", "MZ", ["Aizawl"]),
        ("Nagaland", "NL", ["Kohima", "Dimapur"]),
        ("Odisha", "OD", ["Bhubaneswar", "Cuttack", "Rourkela"]),
        ("Punjab", "PB", ["Ludhiana", "Amritsar", "Jalandhar", "Mohali"]),
        ("Rajasthan", "RJ", ["Jaipur", "Jodhpur", "Udaipur", "Kota"]),
        ("Sikkim", "SK", ["Gangtok"]),
        ("Tamil Nadu", "TN", ["Chennai", "Coimbatore", "Tiruppur", "Madurai"]),
        ("Telangana", "TS", ["Hyderabad", "Warangal"]),
        ("Tripura", "TR", ["Agartala"]),
        ("Uttar Pradesh", "UP", ["Lucknow", "Noida", "Kanpur", "Varanasi", "Agra"]),
        ("Uttarakhand", "UK", ["Dehradun", "Haridwar", "Haldwani"]),
        ("West Bengal", "WB", ["Kolkata", "Siliguri", "Durgapur"]),
        ("Andaman and Nicobar Islands", "AN", ["Port Blair"]),
        ("Chandigarh", "CH", ["Chandigarh"]),
        ("Dadra and Nagar Haveli and Daman and Diu", "DH", ["Daman", "Silvassa"]),
        ("Delhi", "DL", ["New Delhi", "Delhi"]),
        ("Jammu and Kashmir", "JK", ["Srinagar", "Jammu", "Anantnag"]),
        ("Ladakh", "LA", ["Leh", "Kargil"]),
        ("Lakshadweep", "LD", ["Kavaratti"]),
        ("Puducherry", "PY", ["Puducherry"])
    ];

    /// <summary>Cities where textile goods are made and bought in bulk. They are flagged as sourcing hubs; every other city is a delivery city.</summary>
    public static readonly string[] SourcingHubs = ["Surat", "Delhi", "Ludhiana", "Panipat", "Jaipur", "Tiruppur", "Ahmedabad", "Mumbai"];

    public static readonly (string Name, string Prefix)[] Zones =
    [
        ("North", "1"), ("West", "2"), ("North Central", "3"), ("East", "4"),
        ("South Central", "5"), ("South", "6"), ("North West", "7"), ("North East", "8"), ("Remote", "9")
    ];
}
