using System.Globalization;
using System.Text;

namespace Disomat.Core;

public static class DisomatProtocol
{
    // Original 42-byte command used in all three historical clients. Not standard Modbus.
    public const string LegacyRequestHex = "00-00-01-00-00-24-01-55-65-00-04-01-72-70-74-6f-20-42-23-42-23-34-36-23-31-23-34-30-23-32-30-30-31-23-31-23-36-31-21-23-aa-84";

    public static byte[] DecodeHex(string hex)
    {
        ArgumentNullException.ThrowIfNull(hex);
        var clean = new string(hex.Where(c => !char.IsWhiteSpace(c) && c != '-' && c != ':').ToArray());
        if (clean.Length == 0 || clean.Length % 2 != 0) throw new FormatException("Invalid hexadecimal packet length.");
        return Convert.FromHexString(clean);
    }

    // The legacy bytecode_after_place="23 4" captures bytes BETWEEN the 4th and 5th '#'.
    // Full response framing, CRC, and device-status checks require hardware captures.
    public static decimal ParseLegacyWeight(ReadOnlySpan<byte> frame, byte delimiter = 0x23, int field = 4)
    {
        if (field < 1) throw new ArgumentOutOfRangeException(nameof(field));
        int seen = 0, start = -1;
        for (int i = 0; i < frame.Length; i++)
        {
            if (frame[i] != delimiter) continue;
            ++seen;
            if (seen == field) start = i + 1;
            if (seen == field + 1 && start >= 0)
            {
                string raw = Encoding.ASCII.GetString(frame[start..i]).Trim().Replace(" ", "").Replace(',', '.');
                if (!decimal.TryParse(raw, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture, out var weight))
                    throw new FormatException($"Invalid weight field '{raw}'.");
                return weight;
            }
        }
        throw new FormatException("Incomplete response: missing weight field delimiters.");
    }
}
