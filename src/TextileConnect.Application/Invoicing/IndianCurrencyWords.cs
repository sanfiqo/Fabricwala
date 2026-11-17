namespace TextileConnect.Application.Invoicing;

/// <summary>An amount in words as an Indian invoice writes it: lakh and crore, "Rupees ... and ... Paise Only".</summary>
public static class IndianCurrencyWords
{
    private static readonly string[] Small =
    [
        "Zero", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen",
        "Sixteen", "Seventeen", "Eighteen", "Nineteen",
    ];
    private static readonly string[] Tens = ["", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety"];

    public static string Of(decimal amount)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), "An invoice amount cannot be negative.");
        var paiseTotal = (long)decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero);
        var rupees = paiseTotal / 100;
        var paise = (int)(paiseTotal % 100);
        var text = "Rupees " + Number(rupees);
        if (paise > 0) text += " and " + Number(paise) + " Paise";
        return text + " Only";
    }

    private static string Number(long value)
    {
        if (value < 20) return Small[value];
        if (value < 100) return Tens[value / 10] + (value % 10 > 0 ? " " + Small[value % 10] : string.Empty);
        if (value < 1_000) return Join(Small[value / 100] + " Hundred", value % 100);
        if (value < 100_000) return Join(Number(value / 1_000) + " Thousand", value % 1_000);
        if (value < 10_000_000) return Join(Number(value / 100_000) + " Lakh", value % 100_000);
        return Join(Number(value / 10_000_000) + " Crore", value % 10_000_000);
    }

    private static string Join(string head, long rest) => rest == 0 ? head : head + " " + Number(rest);
}
