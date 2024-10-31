using Unity.Mathematics;

public static class Double2BigNumExtensions
{
    // Private constant for precision limitation based on exponent difference
    private const int PrecisionLimit = 11;  // Adjust this based on desired precision range

    // Normalizes the double2 to maintain x between 1 and 10
    public static void NormalizeBigNum(ref this double2 value)
    {
        if (value.x == 0)
        {
            value.y = 0;
            return;
        }

        // Adjust x to be between 1 and 10
        double newX = value.x;
        int exponentAdjustment = 0;

        while (math.abs(newX) >= 10)
        {
            newX /= 10;
            exponentAdjustment++;
        }

        while (math.abs(newX) < 1 && newX != 0)
        {
            newX *= 10;
            exponentAdjustment--;
        }

        value.x = newX;
        value.y += exponentAdjustment;
    }

    public static class BigNum
    {

        public static double2 GetNormalized(double2 value)
        {
            value.NormalizeBigNum();
            return value;
        }

        public static double2 GetNormalized(double x, double y)
        {
            double2 value = new double2(x, y);
            value.NormalizeBigNum();
            return value;
        }
    }

    // Multiply two double2 BigNum values
    public static void MultiplyBigNum(ref this double2 a, double2 b)
    {
        // Multiply mantissas and add exponents
        a.x *= b.x;
        a.y += b.y;

        // Normalize the result
        a.NormalizeBigNum();
    }

    public static double2 MultiplyBigNumR(in this double2 a, in double2 b)
    {
        double2 result = a;
        result.MultiplyBigNum(b);
        return result;
    }

    // Add two double2 BigNum values with precision limitation
    public static void AddBigNum(ref this double2 a, double2 b)
    {
        // Check the difference in exponents
        double exponentDifference = a.y - b.y;

        if (exponentDifference > PrecisionLimit)
        {
            // 'a' is the larger value, b is too small to affect the result
            return;
        }
        if(exponentDifference < -PrecisionLimit)
        {
            // 'b' is the larger value, a is too small to affect the result
            a = b;
            return;
        }

        // Align exponents
        if (exponentDifference >= 0)
        {
            a.x *= math.pow(10, exponentDifference);
            a.y = b.y;
        }
        else
        {
            b.x *= math.pow(10, -exponentDifference);
        }

        // Add mantissas
        a.x += b.x;

        // Normalize the result
        a.NormalizeBigNum();
    }

    public static double2 AddBigNumR(in this double2 a, in double2 b)
    {
        double2 result = a;
        result.AddBigNum(b);
        return result;
    }

    // Subtract two double2 BigNum values with precision limitation
    public static void SubtractBigNum(ref this double2 a, double2 b)
    {
        // Check the difference in exponents
        double exponentDifference = math.abs(a.y - b.y);

        if (exponentDifference > PrecisionLimit)
        {
            // If exponent difference is too large, the smaller value is negligible
            if (a.y > b.y)
            {
                // 'a' is the larger value, b is too small to affect the result
                return;
            }
            else
            {
                // Subtracting something too large will make a negligible (near zero)
                a.x = 0;
                a.y = 0;
                return;
            }
        }

        // Align exponents
        if (a.y > b.y)
        {
            b.x *= math.pow(10, b.y - a.y);
            b.y = a.y;
        }
        else if (a.y < b.y)
        {
            a.x *= math.pow(10, a.y - b.y);
            a.y = b.y;
        }

        // Subtract mantissas
        a.x -= b.x;

        // Normalize the result
        a.NormalizeBigNum();
    }

    public static double2 SubtractBigNumR(in this double2 a, in double2 b)
    {
        double2 result = a;
        result.SubtractBigNum(b);
        return result;
    }

    public static void PowBigNum(ref this double2 a,  double b)
    {
        // Calculate the power of the mantissa
        a.x = math.pow(a.x, b);

        // Calculate the power of the exponent
        a.y *= b;

        // Normalize the result
        a.NormalizeBigNum();
    }

    public static double2 PowBigNumR(in this double2 a, in double b)
    {
        double2 result = a;
        result.PowBigNum(b);
        return result;
    }

    public static bool IsBigNumGreaterThan(in this double2 a, in double2 b)
    {
        var normalizedA = BigNum.GetNormalized(a);
        var normalizedB = BigNum.GetNormalized(b);

        if (normalizedA.y > normalizedB.y)
        {
            return true;
        }
        if (normalizedA.y < normalizedB.y)
        {
            return false;
        }
        return normalizedA.x > normalizedB.x;
    }

    public static bool IsBigNumGreaterOrEqualThan(in this double2 a, in double2 b)
    {
        var normalizedA = BigNum.GetNormalized(a);
        var normalizedB = BigNum.GetNormalized(b);

        return normalizedA.IsBigNumGreaterThan(normalizedB) || normalizedA.Equals(normalizedB);
    }

    public static string ToBigNumString(in this double2 value)
    {
        if(value.y < 6)
        {
            double result = value.x * math.pow(10, value.y);
            return result.ToString("0.###");
        }
        if(value.y < 1_000_000)
        {
            return $"{value.x:F3}e{value.y:F0}";
        }
        var exponent2 = math.floor(math.log10(value.y));
        var y = value.y / math.pow(10, exponent2);
        return $"{value.x:F3}e{y:0.###}e{exponent2:F0}";
    }
}
