using UnityEngine;

public class CameraFreeflight : MonoBehaviour
{
	public float speedNormal = 10f;

	public float speedFast = 50f;

	public float mouseSensitivityX = 5f;

	public float mouseSensitivityY = 5f;

	private float rotY;

	private void Update()
	{
		if (Input.GetMouseButton(1))
		{
			float y = base.transform.localEulerAngles.y + Input.GetAxis("Mouse X") * mouseSensitivityX;
			rotY += Input.GetAxis("Mouse Y") * mouseSensitivityY;
			rotY = Mathf.Clamp(rotY, -89.5f, 89.5f);
			base.transform.localEulerAngles = new Vector3(0f - rotY, y, 0f);
		}
		float axis = Input.GetAxis("Vertical");
		float axis2 = Input.GetAxis("Horizontal");
		if (axis != 0f)
		{
			float num = (Input.GetKey(KeyCode.LeftShift) ? speedFast : speedNormal);
			Vector3 vector = new Vector3(0f, 0f, axis * num * Time.deltaTime);
			base.gameObject.transform.localPosition += base.gameObject.transform.localRotation * vector;
		}
		if (axis2 != 0f)
		{
			float num2 = (Input.GetKey(KeyCode.LeftShift) ? speedFast : speedNormal);
			Vector3 vector2 = new Vector3(axis2 * num2 * Time.deltaTime, 0f, 0f);
			base.gameObject.transform.localPosition += base.gameObject.transform.localRotation * vector2;
		}
	}
}
