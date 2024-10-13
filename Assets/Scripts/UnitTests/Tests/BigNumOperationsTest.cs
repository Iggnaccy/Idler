using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Mathematics;

public class BigNumOperationsTest
{
    private const double tolerance = 1e-10; // Adjust tolerance as needed based on precision requirements
    // A Test behaves as an ordinary method
    [Test]
    public void TestNormalizeBigNum()
    {
        double2 d = new double2(1234567890123456789, 12);
        d.NormalizeBigNum();
        double2 expected = new double2(1.234567890123456789, 30);

        // Use a tolerance to account for floating-point precision errors
        bool xIsClose = math.abs(d.x - expected.x) < tolerance;
        bool yIsClose = math.abs(d.y - expected.y) < tolerance;

        Assert.True(xIsClose && yIsClose);


        d = new double2(1234567890123456789, 0);
        d.NormalizeBigNum();

        expected = new double2(1.234567890123456789, 18);
        xIsClose = math.abs(d.x - expected.x) < tolerance;
        yIsClose = math.abs(d.y - expected.y) < tolerance;

        Assert.True(xIsClose && yIsClose);

        d = new double2(0, 1234567890123456789);
        d.NormalizeBigNum();

        Assert.AreEqual(new double2(0, 0), d);

        d = new double2(0, 0);
        d.NormalizeBigNum();
        Assert.AreEqual(new double2(0, 0), d);
    }

    [Test]
    public void TestAddBigNum()
    {
        double2 a = new double2(123456789, 12);
        a.NormalizeBigNum();
        double2 b = new double2(876543210, 12);
        b.NormalizeBigNum();
        double2 expected = new double2(9.99999999, 20);
        a.AddBigNum(b);

        // Use a tolerance to account for floating-point precision errors
        bool xIsClose = math.abs(a.x - expected.x) < tolerance;
        bool yIsClose = math.abs(a.y - expected.y) < tolerance;

        Assert.True(xIsClose && yIsClose);

        a = new double2(123456789, 12);
        a.NormalizeBigNum();
        b = new double2(876543210, 0);
        b.NormalizeBigNum();
        expected = new double2(1.23456789, 20);
        a.AddBigNum(b);

        xIsClose = math.abs(a.x - expected.x) < tolerance;
        yIsClose = math.abs(a.y - expected.y) < tolerance;

        Assert.True(xIsClose && yIsClose);

        a = new double2(123456789, 0);
        a.NormalizeBigNum();
        b = new double2(876543210, 12);
        b.NormalizeBigNum();
        expected = new double2(8.7654321, 20);
        a.AddBigNum(b);

        xIsClose = math.abs(a.x - expected.x) < tolerance;
        yIsClose = math.abs(a.y - expected.y) < tolerance;

        Assert.True(xIsClose && yIsClose);
    }

    [Test]
    public void TestMultplyBigNum()
    {
        double2 a = new double2(1, 12);
        a.NormalizeBigNum();
        double2 b = new double2(1, 12);
        b.NormalizeBigNum();
        double2 expected = new double2(1, 24);

        a.MultiplyBigNum(b);

        // Use a tolerance to account for floating-point precision errors
        bool xIsClose = math.abs(a.x - expected.x) < tolerance;
        bool yIsClose = math.abs(a.y - expected.y) < tolerance;

        Assert.True(xIsClose && yIsClose);

        a = new double2(1, 12);
        a.NormalizeBigNum();
        b = new double2(1, 0);
        b.NormalizeBigNum();
        expected = new double2(1, 12);
        a.MultiplyBigNum(b);
        
        xIsClose = math.abs(a.x - expected.x) < tolerance;
        yIsClose = math.abs(a.y - expected.y) < tolerance;

        Assert.True(xIsClose && yIsClose);

        a = new double2(1, 0);
        a.NormalizeBigNum();
        b = new double2(1, 12);
        b.NormalizeBigNum();
        expected = new double2(1, 12);
        a.MultiplyBigNum(b);

        xIsClose = math.abs(a.x - expected.x) < tolerance;
        yIsClose = math.abs(a.y - expected.y) < tolerance;

        Assert.True(xIsClose && yIsClose);

        a = new double2(1, 0);
        a.NormalizeBigNum();
        b = new double2(0.25, 0);
        b.NormalizeBigNum();
        expected = new double2(2.5, -1);

        a.MultiplyBigNum(b);

        xIsClose = math.abs(a.x - expected.x) < tolerance;
        yIsClose = math.abs(a.y - expected.y) < tolerance;

        Assert.True(xIsClose && yIsClose);

        a = new double2(15, 10);
        a.NormalizeBigNum();
        b = new double2(0.25, 0);
        b.NormalizeBigNum();
        expected = new double2(3.75, 10);

        a.MultiplyBigNum(b);

        xIsClose = math.abs(a.x - expected.x) < tolerance;
        yIsClose = math.abs(a.y - expected.y) < tolerance;

        Assert.True(xIsClose && yIsClose);
    }

    [Test]
    public void TestChainedAddBigNum()
    {
        double2 d = new double2(2, 0);
        double2 add = new double2(2, 0);
        double2 expected = new double2(2.2, 1); // 22 = 2 + 2 * 10

        for (int i = 0; i < 10; i++)
        {
            d.AddBigNum(add);
        }

        Assert.AreEqual(expected, d);
    }
}
