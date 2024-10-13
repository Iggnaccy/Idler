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
            b.x *= math.pow(10, a.y - b.y);
            b.y = a.y;
        }
        else if (a.y < b.y)
        {
            a.x *= math.pow(10, b.y - a.y);
            a.y = b.y;
        }

        // Subtract mantissas
        a.x -= b.x;

        // Normalize the result
        a.NormalizeBigNum();
    }

    public static string ToBigNumString(in this double2 value)
    {
        return $"{value.x:F4}e{value.y:F0}";
    }
}
