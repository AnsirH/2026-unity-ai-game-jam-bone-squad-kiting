using UnityEngine;
using BoneSquadKiting.Data;
using BoneSquadKiting.Squad;

/// <summary>Shared PlayMode helper: a FormationController with in-memory formation definitions.</summary>
public static class SquadTestRig
{
    public static FormationController MakeFc()
    {
        var fc = new GameObject("fc").AddComponent<FormationController>();
        fc.Formations = new()
        {
            Def("dense", FormationMode.Grid, spacing: 0.53f),
            Def("line", FormationMode.Line, spacing: 0.49f, stagger: 0.12f),
            Def("ring", FormationMode.Ring, radius: 3.7f),
            Def("cross", FormationMode.Cross, spacing: 0.61f),
        };
        fc.Init();
        return fc;
    }

    static FormationDefinition Def(string id, FormationMode mode, float spacing = 0, float radius = 0, float stagger = 0)
    {
        var d = ScriptableObject.CreateInstance<FormationDefinition>();
        d.Id = id; d.Mode = mode; d.Spacing = spacing; d.Radius = radius; d.Stagger = stagger;
        return d;
    }
}
