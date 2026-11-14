internal static class RawAbiCheckSampler
{
    public static IReadOnlyList<RawAbiCheck> Sample(IReadOnlyList<RawAbiCheck> checks, int count)
    {
        if (count <= 0 || checks.Count == 0)
        {
            return [];
        }

        var sample = new List<RawAbiCheck>(count);
        var seenCheckIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var check in checks)
        {
            if (seenCheckIds.Add(check.CheckId))
            {
                sample.Add(check);
                if (sample.Count == count)
                {
                    return sample;
                }
            }
        }

        foreach (var check in checks)
        {
            if (sample.Contains(check))
            {
                continue;
            }

            sample.Add(check);
            if (sample.Count == count)
            {
                break;
            }
        }

        return sample;
    }
}
