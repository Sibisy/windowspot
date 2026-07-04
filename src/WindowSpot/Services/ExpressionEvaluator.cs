using System;
using System.Collections.Generic;
using System.Globalization;

namespace WindowSpot.Services;

/// <summary>
/// "12*(3+4)/2" 같은 산술식을 재귀 하강 파서로 평가한다.
/// 지원 연산자: + - * / % ^ () 및 단항 부호.
/// </summary>
public static class ExpressionEvaluator
{
    public static double Evaluate(string expression)
    {
        var tokens = Tokenize(expression);
        int pos = 0;
        double result = ParseExpression(tokens, ref pos);
        if (pos != tokens.Count) throw new FormatException("Unexpected trailing token.");
        return result;
    }

    private static List<string> Tokenize(string expr)
    {
        var tokens = new List<string>();
        int i = 0;
        while (i < expr.Length)
        {
            char c = expr[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }

            if (char.IsDigit(c) || c == '.')
            {
                int start = i;
                while (i < expr.Length && (char.IsDigit(expr[i]) || expr[i] == '.')) i++;
                tokens.Add(expr[start..i]);
                continue;
            }

            if ("+-*/^%()".IndexOf(c) >= 0)
            {
                tokens.Add(c.ToString());
                i++;
                continue;
            }

            throw new FormatException($"Unexpected character '{c}'.");
        }
        return tokens;
    }

    private static double ParseExpression(List<string> tokens, ref int pos)
    {
        double value = ParseTerm(tokens, ref pos);
        while (pos < tokens.Count && (tokens[pos] == "+" || tokens[pos] == "-"))
        {
            string op = tokens[pos++];
            double rhs = ParseTerm(tokens, ref pos);
            value = op == "+" ? value + rhs : value - rhs;
        }
        return value;
    }

    private static double ParseTerm(List<string> tokens, ref int pos)
    {
        double value = ParseFactor(tokens, ref pos);
        while (pos < tokens.Count && (tokens[pos] == "*" || tokens[pos] == "/" || tokens[pos] == "%"))
        {
            string op = tokens[pos++];
            double rhs = ParseFactor(tokens, ref pos);
            value = op switch
            {
                "*" => value * rhs,
                "/" => value / rhs,
                _ => value % rhs,
            };
        }
        return value;
    }

    private static double ParseFactor(List<string> tokens, ref int pos)
    {
        double value = ParseUnary(tokens, ref pos);
        while (pos < tokens.Count && tokens[pos] == "^")
        {
            pos++;
            double rhs = ParseUnary(tokens, ref pos);
            value = Math.Pow(value, rhs);
        }
        return value;
    }

    private static double ParseUnary(List<string> tokens, ref int pos)
    {
        if (pos < tokens.Count && tokens[pos] == "-")
        {
            pos++;
            return -ParseUnary(tokens, ref pos);
        }
        if (pos < tokens.Count && tokens[pos] == "+")
        {
            pos++;
            return ParseUnary(tokens, ref pos);
        }
        return ParsePrimary(tokens, ref pos);
    }

    private static double ParsePrimary(List<string> tokens, ref int pos)
    {
        if (pos >= tokens.Count) throw new FormatException("Unexpected end of expression.");
        string token = tokens[pos];

        if (token == "(")
        {
            pos++;
            double value = ParseExpression(tokens, ref pos);
            if (pos >= tokens.Count || tokens[pos] != ")") throw new FormatException("Missing closing parenthesis.");
            pos++;
            return value;
        }

        if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double num))
        {
            pos++;
            return num;
        }

        throw new FormatException($"Unexpected token '{token}'.");
    }
}
