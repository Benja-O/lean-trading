namespace Trading.Analytics.Validation;

public sealed record Gate1Result(
    bool Passed,
    IReadOnlyList<(string Name, bool Pass, string Detail)> Checks);

public sealed record Gate2Result(
    bool Passed,
    IReadOnlyList<(string Name, bool Pass, string Detail)> Checks);
