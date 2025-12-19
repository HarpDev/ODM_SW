using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class WeaponUI : MonoBehaviour
{
    [SerializeField]
    WeaponController controller;
    [SerializeField]
    Slider FireIntervalSlider;

    // Start is called before the first frame update
    void Start()
    {
        FireIntervalSlider.value = controller.GetFireIntervalPercent();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void FixedUpdate()
    {
        FireIntervalSlider.value = controller.GetFireIntervalPercent();
    }
}
