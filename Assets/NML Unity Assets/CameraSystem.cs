using UnityEngine;

public class CameraSystem : MonoBehaviour
{
    [SerializeField] private bool useEdgeScrolling = false;
    private bool dragPanMoveActive;
    private Vector2 lastMousePostion;
    private void Update()
    {
        Vector3 inputDir = new Vector3(0, 0, 0);

        if (Input.GetKey(KeyCode.W)) inputDir.z = +1f;
        if (Input.GetKey(KeyCode.S)) inputDir.z = -1f;
        if (Input.GetKey(KeyCode.A)) inputDir.x = -1f;
        if (Input.GetKey(KeyCode.D)) inputDir.x = +1f;
        if (Input.GetKey(KeyCode.Space)) inputDir.y = +1f;
        if (Input.GetKey(KeyCode.LeftControl)) inputDir.y = -1f;

        if (useEdgeScrolling)
        {
            int edgeScrollSize = 20;

            if (Input.mousePosition.x < edgeScrollSize) inputDir.x = -1f;
            if (Input.mousePosition.y < edgeScrollSize) inputDir.z = -1f;
            if (Input.mousePosition.x > Screen.width - edgeScrollSize) inputDir.x = +1f;
            if (Input.mousePosition.y > Screen.width - edgeScrollSize) inputDir.z = +1f;
        }

        float moveSpeed = 10f;
        transform.position += inputDir * moveSpeed * Time.deltaTime;

        if (Input.GetMouseButtonDown(1))
        {
            dragPanMoveActive = true;
            lastMousePostion = Input.mousePosition;
        }
        if (Input.GetMouseButtonUp(1))
        {
            dragPanMoveActive = false;
        }

        if (dragPanMoveActive)
        {
            Vector2 mouseMovementDelta = (Vector2)Input.mousePosition - lastMousePostion;

            float dragPanSpeed = 2f;
            inputDir.x = mouseMovementDelta.x * dragPanSpeed;
            inputDir.z = mouseMovementDelta.y * dragPanSpeed;

            lastMousePostion = Input.mousePosition;
        }

        Vector3 moveDir = transform.forward * inputDir.z + transform.right * inputDir.x;

        float rotateDir = 0f;
        if (Input.GetKey(KeyCode.Q)) rotateDir = +1f;
        if (Input.GetKey(KeyCode.E)) rotateDir = -1f;

        float rotateSpeed = 50f;

        transform.eulerAngles += new Vector3(0, rotateDir * rotateSpeed * Time.deltaTime, 0);

    }


}
