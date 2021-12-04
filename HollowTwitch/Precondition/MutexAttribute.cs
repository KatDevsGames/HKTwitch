using System.Collections.Generic;
using HollowTwitch.Entities.Attributes;

namespace HollowTwitch.Precondition;

public class MutexAttribute : PreconditionAttribute
{
    //we're assuming this is going to get called single-threaded from unity
    private static readonly HashSet<string> _mutexes = new();

    public readonly string Mutex;

    public MutexAttribute(string mutex) => Mutex = mutex;

    public override bool Check(string user) => !_mutexes.Contains(Mutex);

    public override void Use() => _mutexes.Add(Mutex);

    public override void Reset() => _mutexes.Remove(Mutex);
}
