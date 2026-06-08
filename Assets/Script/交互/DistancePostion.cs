using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[ExecuteInEditMode]

public class DistancePostion : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    public Transform footTransform; // 在 Inspector 里把 FootAnchor 拖进来

    void Update()
    {
        // 传给 Shader 脚底的坐标，而不是角色肚子的坐标
        if (footTransform != null)
            Shader.SetGlobalVector("_Position", footTransform.position);
    }
}
