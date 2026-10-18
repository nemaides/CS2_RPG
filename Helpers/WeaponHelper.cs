namespace CS2_RPG.Helpers;

public static class WeaponHelper
{
    public static string GetCleanWeaponName(string weapon)
    {
        if (string.IsNullOrWhiteSpace(weapon)) return "Unknown";

        string cleanKey = weapon.ToLower().Trim();
        if (cleanKey.StartsWith("weapon_"))
        {
            cleanKey = cleanKey.Replace("weapon_", "");
        }

        return cleanKey switch
        {
            "elite" => "Dual Berettas",
            "knife" or "bayonet" => "Knife",
            var w when w.StartsWith("knife_") => "Knife",
            "ak47" => "AK-47",
            "m4a1" => "M4A1",
            "m4a1_silencer" or "m4a1_s" => "M4A1-S",
            "usp_silencer" or "usp_s" => "USP-S",
            "glock" => "Glock-18",
            "deagle" => "Desert Eagle",
            "mp5sd" => "MP5-SD",
            "ssg08" => "SSG 08",
            "sg553" => "SG 553",
            "hkp2000" or "p2000" => "P2000",
            "mac10" => "MAC-10",
            "p90" => "P90",
            "fiveseven" => "Five-SeveN",
            "cz75a" => "CZ75-Auto",
            "awp" => "AWP",
            "famas" => "FAMAS",
            "galilar" => "Galil AR",
            "aug" => "AUG",
            "g3sg1" => "G3SG1",
            "scar20" => "SCAR-20",
            "p250" => "P250",
            "tec9" => "Tec-9",
            "revolver" => "R8 Revolver",
            "mp9" => "MP9",
            "mp7" => "MP7",
            "ump45" => "UMP-45",
            "bizon" => "PP-Bizon",
            "nova" => "Nova",
            "xm1014" => "XM1014",
            "mag7" => "MAG-7",
            "sawedoff" => "Sawed-Off",
            "negev" => "Negev",
            "m249" => "M249",
            "hegrenade" => "HE Grenade",
            "inferno" or "molotov" => "Molotov",
            "smokegrenade" => "Smoke Grenade",
            "flashbang" => "Flashbang",
            "decoy" => "Decoy Grenade",
            "taser" => "Zeus x27",
            _ => cleanKey.Length > 0 ? char.ToUpper(cleanKey[0]) + cleanKey[1..] : "Unknown"
        };
    }
}