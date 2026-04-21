using UnityEngine;

public class MovingPlatform : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D collision) {
        if (collision.name == "AfterImageReplay") {
            collision.transform.SetParent(transform); 
        }
    }

    private void OnTriggerExit2D(Collider2D collision) {
        if (collision.name == "AfterImageReplay") {
            collision.transform.SetParent(null);
        }
    }
}
