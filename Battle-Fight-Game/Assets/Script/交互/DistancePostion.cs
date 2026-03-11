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

    // Update is called once per frame
    void Update()
    {
        Shader.SetGlobalVector("_Position", transform.position);//将当前物体的位置传递给全局着色器变量"_Position"，可以在着色器中使用这个变量来进行相关的计算，例如实现基于距离的效果等
    }
}
