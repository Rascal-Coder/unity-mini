using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways, RequireComponent(typeof(SortingGroup))]
public sealed class BattleDepth : MonoBehaviour {
    public int offset;
    SortingGroup group;
    void OnEnable(){Apply();}
    void LateUpdate(){Apply();}
    void Apply(){if(group==null)group=GetComponent<SortingGroup>();group.sortingOrder=Mathf.RoundToInt(-transform.position.y*100)+offset;}
}
