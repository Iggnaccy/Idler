using Godot;
using System;
using System.Threading;
using System.Collections.Generic;

public partial class PerformanceTest : Node
{
    // Batch sizes to test
    private readonly int[] batchSizes = { 100, 10000, 1000000, 10000000 };

    private readonly Random random = new Random();
    public override void _Ready()
    {
        // Start the performance tests on a new thread
        Thread testThread = new Thread(RunPerformanceTests);
        testThread.Start();
    }

    private void RunPerformanceTests()
    {
        foreach (int batchSize in batchSizes)
        {
            // Prepare results list
            List<string> results = new List<string>();

            results.Add($"--- Testing with batch size: {batchSize} ---");

            // Double tests
            results.Add("Double Tests:");
            double[] doubleNumbersA, doubleNumbersB;
            double doubleInitTime = TestDoubleInitialization(batchSize, out doubleNumbersA, out doubleNumbersB);
            results.Add($"Initialization time: {doubleInitTime} ms");

            double doubleOpTime = TestDoubleOperations(batchSize, doubleNumbersA, doubleNumbersB);
            results.Add($"Operations time: {doubleOpTime} ms");

            // BigNumber tests
            results.Add("BigNumber Tests:");
            BigNumber[] bigNumberNumbersA, bigNumberNumbersB;
            double bigNumberInitTime = TestBigNumberInitialization(batchSize, out bigNumberNumbersA, out bigNumberNumbersB);
            results.Add($"Initialization time: {bigNumberInitTime} ms");

            double bigNumberOpTime = TestBigNumberOperations(batchSize, bigNumberNumbersA, bigNumberNumbersB);
            results.Add($"Operations time: {bigNumberOpTime} ms");

            results.Add("");

            // Safely print results on the main thread
            foreach (string result in results)
            {
                // Use CallDeferred to schedule the GD.Print call on the main thread
                CallDeferred(nameof(PrintResult), result);
            }

            // Free memory
            doubleNumbersA = null;
            doubleNumbersB = null;
            bigNumberNumbersA = null;
            bigNumberNumbersB = null;
            GC.Collect();
        }
    }

    private void PrintResult(string result)
    {
        GD.Print(result);
    }

    private double TestDoubleInitialization(int batchSize, out double[] numbersA, out double[] numbersB)
    {
        numbersA = new double[batchSize];
        numbersB = new double[batchSize];

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Generate random numbers
        for (int i = 0; i < batchSize; i++)
        {
            numbersA[i] = random.NextDouble() * 1e6;
            numbersB[i] = random.NextDouble() * 1e6;
        }

        stopwatch.Stop();
        return stopwatch.Elapsed.TotalMilliseconds;
    }

    private double TestDoubleOperations(int batchSize, double[] numbersA, double[] numbersB)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Perform operations
        for (int i = 0; i < batchSize; i++)
        {
            double a = numbersA[i];
            double b = numbersB[i];

            double resultAdd = a + b;
            double resultSub = a - b;
            double resultMul = a * b;
            double resultDiv = a / (b + 1); // Avoid division by zero
            double resultPow = Math.Pow(a, 1.001);
        }

        stopwatch.Stop();
        return stopwatch.Elapsed.TotalMilliseconds;
    }

    private double TestBigNumberInitialization(int batchSize, out BigNumber[] numbersA, out BigNumber[] numbersB)
    {
        numbersA = new BigNumber[batchSize];
        numbersB = new BigNumber[batchSize];

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Generate random BigNumbers
        for (int i = 0; i < batchSize; i++)
        {
            double valueA = random.NextDouble() * 1e6;
            double valueB = random.NextDouble() * 1e6;
            double expA = random.NextDouble() * 1e6;
            double expB = random.NextDouble() * 1e6;
            numbersA[i] = new BigNumber(valueA, expA);
            numbersB[i] = new BigNumber(valueB, expB);
        }

        stopwatch.Stop();
        return stopwatch.Elapsed.TotalMilliseconds;
    }

    private double TestBigNumberOperations(int batchSize, BigNumber[] numbersA, BigNumber[] numbersB)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Perform operations
        for (int i = 0; i < batchSize; i++)
        {
            BigNumber a = numbersA[i];
            BigNumber b = numbersB[i];

            a.AddInPlace(b);
            a.SubtractInPlace(b);
            a.MultiplyInPlace(b);
            a.DivideInPlace(b);
            a.PowInPlace(1.001);
        }

        stopwatch.Stop();
        return stopwatch.Elapsed.TotalMilliseconds;
    }

}
