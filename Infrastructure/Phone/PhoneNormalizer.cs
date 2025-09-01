using System.Linq;
using PhoneNumbers;

namespace Infrastructure.Phone;

public class PhoneNormalizer
{
    private readonly PhoneNumberUtil _util = PhoneNumberUtil.GetInstance();

    public string Normalize(string input)
    {
        try
        {
            var number = _util.Parse(input, "IN");
            return _util.Format(number, PhoneNumberFormat.E164);
        }
        catch
        {
            return new string(input.Where(char.IsDigit).ToArray());
        }
    }
}
