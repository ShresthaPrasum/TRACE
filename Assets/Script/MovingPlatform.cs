using UnityEngine;

public class MovingPlatform : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D collision) 
    {

        if (collision.name.Contains("AfterImageReplay")) 
        {
            collision.transform.SetParent(transform); 
        }
    }
}