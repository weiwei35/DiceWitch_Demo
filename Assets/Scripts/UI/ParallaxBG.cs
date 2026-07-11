using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParallaxBG : MonoBehaviour
{
    //图片跟随鼠标移动
    //图片跟随比例可控，不同层级图片跟随距离不同，实现视差

    [Range(0,10)]//控制输入范围
    public float followRatio = 0.1f;
    public float parallaxSpeed = 0.1f;
    private RectTransform rectTransform;
    private Vector2 lastMousePosition;//用于记录鼠标上次的位置，会与当前位置对比，取得鼠标移动距离

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    private void Start()
    {
        lastMousePosition = Input.mousePosition;
    }

    private void Update()
    {
        Vector2 currentMousePos = Input.mousePosition;
        Vector2 distance = currentMousePos - lastMousePosition;
        //根据设置的比例获取图片位移
        float offsetX = distance.x * followRatio*parallaxSpeed;
        float offsetY = distance.y * followRatio*parallaxSpeed;
        rectTransform.anchoredPosition += new Vector2(offsetX, offsetY);
        lastMousePosition = currentMousePos;
    }
}
