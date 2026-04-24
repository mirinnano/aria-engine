using System;

namespace AriaEngine.Core;

public enum RegisterStorageScope
{
    Volatile,
    Save,
    Persistent
}

public static class RegisterStoragePolicy
{
    public static RegisterStorageScope Classify(string registerName)
    {
        string name = Normalize(registerName);
        if (name.StartsWith("p.", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("sys.", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("global.", StringComparison.OrdinalIgnoreCase))
        {
            return RegisterStorageScope.Persistent;
        }

        if (name.StartsWith("v.", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("tmp.", StringComparison.OrdinalIgnoreCase) ||
            name is "0" or "r0")
        {
            return RegisterStorageScope.Volatile;
        }

        if (int.TryParse(name, out int number))
        {
            if (number <= 99) return RegisterStorageScope.Volatile;
            if (number is >= 100 and <= 199) return RegisterStorageScope.Persistent;
            if (number is >= 300 and <= 499) return RegisterStorageScope.Persistent;
            return RegisterStorageScope.Save;
        }

        return RegisterStorageScope.Save;
    }

    public static bool IsPersistent(string registerName) => Classify(registerName) == RegisterStorageScope.Persistent;

    public static bool IsSaveStored(string registerName) => Classify(registerName) == RegisterStorageScope.Save;

    public static string Normalize(string registerName)
    {
        return registerName.TrimStart('%').ToLowerInvariant();
    }
}
