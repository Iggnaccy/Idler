using System;

public struct BigNumber : IEquatable<BigNumber>
{
    private const int Precision = 14;
    private double _mantissa;
    private double _exponent; // Treated as an integer value, double is used for increased range

    public BigNumber(double mantissa, double exponent)
    {
        _mantissa = mantissa;
        _exponent = exponent;
        Fix();
    }

    public BigNumber(double value)
    {
        _mantissa = value;
        _exponent = 0;
        Fix();
    }

    private void Fix()
    {
        if (_mantissa == 0)
        {
            _exponent = 0;
            return;
        }

        // Combine any fractional part of the exponent into the mantissa
        double fractionalExponent = _exponent - Math.Floor(_exponent);
        if (fractionalExponent != 0)
        {
            _mantissa *= Math.Pow(10, fractionalExponent);
            _exponent = Math.Floor(_exponent);
        }

        int exp = (int)Math.Log10(Math.Abs(_mantissa));

        _mantissa /= Math.Pow(10, exp);
        _exponent += exp;

        // Adjust mantissa and exponent if mantissa is out of bounds
        if (_mantissa >= 10)
        {
            _mantissa /= 10;
            _exponent += 1;
        }
        else if (_mantissa < 1 && _mantissa != 0)
        {
            _mantissa *= 10;
            _exponent -= 1;
        }

        // Round mantissa to desired precision
        _mantissa = Math.Round(_mantissa, Precision); // Adjust precision as needed
    }


    // Addition
    public static BigNumber operator +(BigNumber a, BigNumber b)
    {
        if (a._mantissa == 0) return b;
        if (b._mantissa == 0) return a;

        BigNumber result;

        // Align exponents (as integers)
        double expDiff = a._exponent - b._exponent;

        if (expDiff > Precision)
        {
            // b is negligible compared to a
            result = a;
        }
        else if (expDiff < -Precision)
        {
            // a is negligible compared to b
            result = b;
        }
        else
        {
            // Adjust mantissas to align exponents
            double alignedMantissaA = a._mantissa * Math.Pow(10, expDiff);
            result = new BigNumber(alignedMantissaA + b._mantissa, b._exponent);
        }

        result.Fix();
        return result;
    }
    public void AddInPlace(BigNumber other)
    {
        // Align exponents
        double expDiff = _exponent - other._exponent;

        if (expDiff > Precision)
        {
            // 'other' is negligible compared to 'this', no change needed
            return;
        }
        if (expDiff < -Precision)
        {
            // 'this' is negligible compared to 'other'
            _mantissa = other._mantissa;
            _exponent = other._exponent;
            return;
        }
        
        // Adjust mantissas to align exponents
        double alignedMantissaOther = other._mantissa * Math.Pow(10, -expDiff);
        _mantissa += alignedMantissaOther;
        
        Fix();
    }


    // Subtraction
    public static BigNumber operator -(BigNumber a, BigNumber b)
    {
        if (b._mantissa == 0) return a;
        if (a._mantissa == 0 || b > a)
        {
            throw new InvalidOperationException("Cannot subtract a larger number from a smaller number (BigNumber is non-negative)");
        }


        BigNumber result;

        // Align exponents (as integers)
        double expDiff = a._exponent - b._exponent;

        if (expDiff > Precision)
        {
            // b is negligible compared to a
            result = a;
            return result;
        }
        else
        {
            // Adjust mantissas to align exponents
            double alignedMantissaA = a._mantissa * Math.Pow(10, expDiff);
            result = new BigNumber(alignedMantissaA - b._mantissa, b._exponent);
        }

        result.Fix();
        return result;
    }

    public void SubtractInPlace(BigNumber other)
    {
        if(other > this)
        {
            // We do not support negative numbers
            throw new InvalidOperationException("Cannot subtract a larger number from a smaller number (BigNumber is non-negative)");
        }

        // Align exponents
        double expDiff = _exponent - other._exponent;

        if (expDiff > Precision)
        {
            // 'other' is negligible compared to 'this', no change needed
            return;
        }
        
        // Adjust mantissas to align exponents
        double alignedMantissaOther = other._mantissa * Math.Pow(10, -expDiff);
        _mantissa -= alignedMantissaOther;
        
        Fix();
    }

    // Multiplication
    public static BigNumber operator *(BigNumber a, BigNumber b)
    {
        BigNumber result = new BigNumber(a._mantissa * b._mantissa, a._exponent + b._exponent);
        result.Fix();
        return result;
    }

    public void MultiplyInPlace(BigNumber other)
    {
        _mantissa *= other._mantissa;
        _exponent += other._exponent;
        Fix();
    }

    // Division
    public static BigNumber operator /(BigNumber a, BigNumber b)
    {
        if (b._mantissa == 0)
            throw new DivideByZeroException("Cannot divide by zero BigNumber");

        BigNumber result = new BigNumber(a._mantissa / b._mantissa, a._exponent - b._exponent);
        result.Fix();
        return result;
    }

    public void DivideInPlace(BigNumber other)
    {
        if(other._mantissa == 0)
            throw new DivideByZeroException("Cannot divide by zero BigNumber");

        _mantissa /= other._mantissa;
        _exponent -= other._exponent;
        Fix();
    }

    // Comparisons
    public static bool operator >(BigNumber a, BigNumber b)
    {
        if (a._exponent != b._exponent)
            return a._exponent > b._exponent;

        return a._mantissa > b._mantissa;
    }


    public static bool operator <(BigNumber a, BigNumber b)
    {
        return b > a;
    }

    public static bool operator >=(BigNumber a, BigNumber b)
    {
        return !(a < b);
    }

    public static bool operator <=(BigNumber a, BigNumber b)
    {
        return !(a > b);
    }

    public static bool operator ==(BigNumber a, BigNumber b)
    {
        return a._exponent == b._exponent && Math.Abs(a._mantissa - b._mantissa) < 1e-14;
    }


    public static bool operator !=(BigNumber a, BigNumber b)
    {
        return !(a == b);
    }

    // Override Equals and GetHashCode
    public override bool Equals(object obj)
    {
        if (!(obj is BigNumber other))
            return false;

        return this == other;
    }

    public bool Equals(BigNumber other)
    {
        return this == other;
    }

    public override int GetHashCode()
    {
        return _mantissa.GetHashCode() ^ _exponent.GetHashCode();
    }

    // ToString method for display
    public override string ToString()
    {
        return FormatBigNumber(this);
    }

    private string FormatBigNumber(BigNumber number, bool isExponent = false)
    {
        if (number._mantissa == 0)
            return "0";

        // Case 1: Exponent ≤ 9 and ≥ -9
        if (number._exponent <= 9 && number._exponent >= -9)
        {
            double value = number._mantissa * Math.Pow(10, number._exponent);

            // Format with up to two decimal points, omit decimals if not needed
            string format = (value % 1 == 0) ? "{0:n0}" : "{0:n2}";
            return string.Format(format, value);
        }
        // Case 2: Exponent between 10 and 999,999
        else if (number._exponent < 1_000_000 && number._exponent > -1_000_000)
        {
            // For exponents, round mantissa to integer if isExponent is true
            string mantissaStr;
            if (isExponent)
                mantissaStr = Math.Round(number._mantissa).ToString("F0");
            else
                mantissaStr = number._mantissa.ToString("0.##");

            // Format exponent without decimal points
            string exponentStr = number._exponent.ToString("F0");
            return $"{mantissaStr}e{exponentStr}";
        }
        // Case 3: Exponent ≥ 1,000,000 or ≤ -1,000,000
        else
        {
            // Format exponent as a BigNumber recursively
            BigNumber exponentBN = new BigNumber(number._exponent, 0);

            // When formatting exponentBN, set isExponent = true
            string exponentStr = FormatBigNumber(exponentBN, true);

            // For exponents, round mantissa to integer if isExponent is true
            string mantissaStr;
            if (isExponent)
                mantissaStr = Math.Round(number._mantissa).ToString("F0");
            else
                mantissaStr = number._mantissa.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

            return $"{mantissaStr}e{exponentStr}";
        }
    }

    // Logarithm (base 10)
    public double Log10()
    {
        return Math.Log10(_mantissa) + _exponent;
    }

    // Natural logarithm
    public double Ln()
    {
        return Math.Log(_mantissa) + _exponent * Math.Log(10);
    }

    // Exponentiation (power)
    public BigNumber Pow(double power)
    {
        double newExponent = _exponent * power;
        double newMantissa = Math.Pow(_mantissa, power);
        BigNumber result = new BigNumber(newMantissa, newExponent);
        result.Fix();
        return result;
    }

    public BigNumber PowInPlace(double power)
    {
        // Since _mantissa > 0 and power > 0
        _exponent *= power;
        _mantissa = Math.Pow(_mantissa, power);
        Fix();
        return this;
    }


    public static BigNumber Pow(double number, double power)
    {
        return new BigNumber(number).Pow(power);
    }

    // Explicit conversion to double (may lose precision)
    public static explicit operator double(BigNumber a)
    {
        return a._mantissa * Math.Pow(10, a._exponent);
    }

    // Implicit conversion from double
    public static implicit operator BigNumber(double value)
    {
        return new BigNumber(value);
    }
}
