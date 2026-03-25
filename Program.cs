using System;
public class Program {
    public static void Main() {
        float t = 0f;
        for (int i = 0; i <= 10; i++) {
            t = i * 0.1f;
            Console.WriteLine($"{i}: t={t:R}, t == {(float)i/10f:R} is {t == (float)i/10f}");
        }
        Console.WriteLine($"10 * 0.1f == 1.0f: {10 * 0.1f == 1.0f}");
        Console.WriteLine($"1.0f - 1.0f: {1.0f - 1.0f}");
    }
}
