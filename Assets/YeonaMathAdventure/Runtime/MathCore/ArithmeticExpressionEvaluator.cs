using System;
using System.Collections.Generic;
using System.Globalization;

namespace YeonaMathAdventure.MathCore
{
    public struct RationalNumber : IEquatable<RationalNumber>
    {
        public readonly long Numerator;
        public readonly long Denominator;

        public RationalNumber(long numerator, long denominator)
        {
            if (denominator == 0L)
            {
                throw new DivideByZeroException();
            }

            if (denominator < 0L)
            {
                numerator = checked(-numerator);
                denominator = checked(-denominator);
            }

            long divisor = GreatestCommonDivisor(Absolute(numerator), denominator);
            Numerator = numerator / divisor;
            Denominator = denominator / divisor;
        }

        public static RationalNumber FromInteger(long value)
        {
            return new RationalNumber(value, 1L);
        }

        public static RationalNumber Add(RationalNumber left, RationalNumber right)
        {
            return new RationalNumber(
                checked(left.Numerator * right.Denominator + right.Numerator * left.Denominator),
                checked(left.Denominator * right.Denominator));
        }

        public static RationalNumber Subtract(RationalNumber left, RationalNumber right)
        {
            return new RationalNumber(
                checked(left.Numerator * right.Denominator - right.Numerator * left.Denominator),
                checked(left.Denominator * right.Denominator));
        }

        public static RationalNumber Multiply(RationalNumber left, RationalNumber right)
        {
            return new RationalNumber(
                checked(left.Numerator * right.Numerator),
                checked(left.Denominator * right.Denominator));
        }

        public static RationalNumber Divide(RationalNumber left, RationalNumber right)
        {
            if (right.Numerator == 0L)
            {
                throw new DivideByZeroException();
            }

            return new RationalNumber(
                checked(left.Numerator * right.Denominator),
                checked(left.Denominator * right.Numerator));
        }

        public bool Equals(RationalNumber other)
        {
            return Numerator == other.Numerator && Denominator == other.Denominator;
        }

        public override bool Equals(object obj)
        {
            return obj is RationalNumber && Equals((RationalNumber)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Numerator.GetHashCode() * 397) ^ Denominator.GetHashCode();
            }
        }

        public override string ToString()
        {
            if (Denominator == 1L)
            {
                return Numerator.ToString(CultureInfo.InvariantCulture);
            }

            return Numerator.ToString(CultureInfo.InvariantCulture) + "/" +
                   Denominator.ToString(CultureInfo.InvariantCulture);
        }

        private static long Absolute(long value)
        {
            if (value == long.MinValue)
            {
                throw new OverflowException();
            }

            return value < 0L ? -value : value;
        }

        private static long GreatestCommonDivisor(long left, long right)
        {
            while (right != 0L)
            {
                long remainder = left % right;
                left = right;
                right = remainder;
            }

            return left == 0L ? 1L : left;
        }
    }

    public sealed class ExpressionEvaluation
    {
        public RationalNumber value;
        public int[] usedNumbers;
        public ArithmeticOperator[] usedOperators;
    }

    public static class ArithmeticExpressionEvaluator
    {
        public const int MaximumExpressionLength = 256;

        public static bool TryEvaluate(string expression, out ExpressionEvaluation evaluation, out string errorCode)
        {
            evaluation = null;
            errorCode = string.Empty;

            if (string.IsNullOrWhiteSpace(expression))
            {
                errorCode = "expression_empty";
                return false;
            }

            if (expression.Length > MaximumExpressionLength)
            {
                errorCode = "expression_too_long";
                return false;
            }

            try
            {
                Parser parser = new Parser(expression);
                RationalNumber value = parser.Parse();
                evaluation = new ExpressionEvaluation
                {
                    value = value,
                    usedNumbers = parser.Numbers.ToArray(),
                    usedOperators = parser.Operators.ToArray()
                };
                return true;
            }
            catch (ExpressionParseException exception)
            {
                errorCode = exception.Code;
                return false;
            }
            catch (DivideByZeroException)
            {
                errorCode = "division_by_zero";
                return false;
            }
            catch (OverflowException)
            {
                errorCode = "number_overflow";
                return false;
            }
        }

        private sealed class Parser
        {
            private readonly string text;
            private int position;

            public readonly List<int> Numbers = new List<int>();
            public readonly List<ArithmeticOperator> Operators = new List<ArithmeticOperator>();

            public Parser(string text)
            {
                this.text = text;
            }

            public RationalNumber Parse()
            {
                RationalNumber result = ParseExpression();
                SkipWhitespace();
                if (position != text.Length)
                {
                    throw new ExpressionParseException("unexpected_token");
                }

                return result;
            }

            private RationalNumber ParseExpression()
            {
                RationalNumber result = ParseTerm();
                while (true)
                {
                    SkipWhitespace();
                    if (Match('+'))
                    {
                        Operators.Add(ArithmeticOperator.Add);
                        result = RationalNumber.Add(result, ParseTerm());
                    }
                    else if (Match('-'))
                    {
                        Operators.Add(ArithmeticOperator.Subtract);
                        result = RationalNumber.Subtract(result, ParseTerm());
                    }
                    else
                    {
                        return result;
                    }
                }
            }

            private RationalNumber ParseTerm()
            {
                RationalNumber result = ParsePrimary();
                while (true)
                {
                    SkipWhitespace();
                    if (Match('*') || Match('×'))
                    {
                        Operators.Add(ArithmeticOperator.Multiply);
                        result = RationalNumber.Multiply(result, ParsePrimary());
                    }
                    else if (Match('/') || Match('÷'))
                    {
                        Operators.Add(ArithmeticOperator.Divide);
                        result = RationalNumber.Divide(result, ParsePrimary());
                    }
                    else
                    {
                        return result;
                    }
                }
            }

            private RationalNumber ParsePrimary()
            {
                SkipWhitespace();
                if (Match('('))
                {
                    RationalNumber nested = ParseExpression();
                    SkipWhitespace();
                    if (!Match(')'))
                    {
                        throw new ExpressionParseException("missing_closing_parenthesis");
                    }

                    return nested;
                }

                if (position >= text.Length || !IsAsciiDigit(text[position]))
                {
                    throw new ExpressionParseException("number_expected");
                }

                long value = 0L;
                while (position < text.Length && IsAsciiDigit(text[position]))
                {
                    int digit = text[position] - '0';
                    value = checked(value * 10L + digit);
                    position++;
                }

                if (value > int.MaxValue)
                {
                    throw new OverflowException();
                }

                Numbers.Add((int)value);
                return RationalNumber.FromInteger(value);
            }

            private void SkipWhitespace()
            {
                while (position < text.Length && char.IsWhiteSpace(text[position]))
                {
                    position++;
                }
            }

            private bool Match(char value)
            {
                if (position < text.Length && text[position] == value)
                {
                    position++;
                    return true;
                }

                return false;
            }

            private static bool IsAsciiDigit(char value)
            {
                return value >= '0' && value <= '9';
            }
        }

        private sealed class ExpressionParseException : Exception
        {
            public readonly string Code;

            public ExpressionParseException(string code)
            {
                Code = code;
            }
        }
    }
}
