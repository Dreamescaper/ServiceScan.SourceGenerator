using System.Collections.Immutable;

namespace DependencyInjection.SourceGenerator;

class EquatableArray<T>
{
    public ImmutableArray<T> Items { get; set; }

    public override bool Equals(object obj)
    {
        if (obj is not EquatableArray<T> other)
            return false;

        if (Items.Length != other.Items.Length)
            return false;

        for (int i = 0; i < Items.Length; i++)
        {
            if (!Equals(Items[i], other.Items[i]))
                return false;
        }

        return true;
    }

    public override int GetHashCode()
    {
        int hashCode = 0;

        for (int i = 0; i < Items.Length; i++)
            hashCode ^= Items[i].GetHashCode();

        return hashCode;
    }
}