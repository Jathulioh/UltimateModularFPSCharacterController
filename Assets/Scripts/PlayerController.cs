using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor.Experimental.GraphView;
using UnityEditor;
#endif

public class PlayerController : MonoBehaviour
{

	// Default settings
	public bool canLookAround = true;
	public bool canMove = true;
	// Additional features foldout
	public bool showAdditionalFeatures = true;
	// Additional features
	public bool canRun =	false;
	public bool canWalk =	false;
	public bool canJump =	false;
	public bool canCrouch = false;
	public bool canProne =	false;
	public bool canLean =	false;

	//[Header("Components")]
	public Rigidbody rb;
	public CapsuleCollider col;
	public GameObject cameraTransform;
	//[Header("Rotation")]
	public float rotateSpeed = 1f;
	public float maxUp = 85f;
	public float maxDown = 85f;
	private Vector3 rot;
	//[Header("Movement")]
	public float proneSpeed = 0.5f;
	public float crouchSpeed = 1f;
	public float walkSpeed = 2f;
	public float moveSpeed = 3f;
	public float sprintSpeed = 6f;
	private float targetSpeed;
	[Range(0.0f, 1.0f)]
	public float smoothing = 0.1f;
	private Vector3 moveDir;
	private bool isSprinting;
	private bool isWalking;
	//[Header("Jumping")]
	public float airSpeed = 1f;
	public float jumpForce = 5f;

	private float currentHeightTransitionSpeed;
	//[Header("Crouching")]
	public float crouchingHeight;
	public float cameraCrouchHeight;
	public float standToCrouchTime = 0.5f;
	private float standingHeight;
	private float cameraStandingHeight;
	private float currentCameraHeight;
	private bool isCrouched;
	private Coroutine heighTransitionCoroutine;

	//[Header("Prone")]
	public float proneHeight;
	public float cameraProneHeight;
	public float standToProneTime = 1.0f;
	public float crouchToProneTime = 0.5f;
	private bool isProne;

	//[Header("Gravity Stuff")

	[Header("Debug")]
	public float velocity;
	private Vector3 currentVelocity;
	private RaycastHit hitInfo;

	private void Awake()
	{
		rb = GetComponent<Rigidbody>();
		col = GetComponent<CapsuleCollider>();

		Cursor.lockState = CursorLockMode.Locked;
		Cursor.visible = false;

		standingHeight = col.height;
		cameraStandingHeight = cameraTransform.transform.localPosition.y;
		currentCameraHeight = cameraStandingHeight;
	}


	private void Start()
	{

	}

	private void Update()
	{
		Rotation();

		velocity = new Vector3(rb.velocity.x,0,rb.velocity.z).magnitude;
		float hori = Input.GetAxisRaw("Horizontal");
		float veri = Input.GetAxisRaw("Vertical");
		Vector3 targetDir = new Vector3(hori, 0, veri).normalized;
		moveDir = Vector3.SmoothDamp(moveDir, targetDir, ref currentVelocity, smoothing);

		if (Input.GetKeyDown(KeyCode.Space) && canJump)
			Jump();

		if (Input.GetKey(KeyCode.LeftShift) && canRun)
			isSprinting = true;
		else
			isSprinting = false;

		if (Input.GetKeyDown(KeyCode.LeftControl) && canCrouch)
			Crouch();

		if (Input.GetKeyDown(KeyCode.X) && canProne)
			Prone();

		if (Input.GetKey(KeyCode.LeftAlt) && canWalk)
			isWalking = true;
		else
			isWalking = false;
	}

	private void LateUpdate()
	{
		cameraTransform.transform.position = new Vector3(transform.position.x, transform.position.y + currentCameraHeight, transform.position.z);
	}

	private void FixedUpdate()
	{
		Movement();
	}

	private void Movement()
	{

		targetSpeed = SpeedControl();

		Vector3 targetVelocity = cameraTransform.transform.TransformDirection(moveDir) * targetSpeed;
		Vector3 velocityChange = targetVelocity - rb.velocity;
		velocityChange.y = 0f;
		velocityChange = Vector3.ClampMagnitude(velocityChange, 1000);
		if (isGrounded())
		{
			rb.AddForce(velocityChange, ForceMode.VelocityChange);
		}
		else
		{
			rb.AddForce(transform.up * Physics.gravity.y, ForceMode.Acceleration);
			rb.AddForce(velocityChange, ForceMode.Acceleration);
		}
	}

	private void Rotation()
	{
		rot.y = Input.GetAxis("Mouse X") * rotateSpeed;
		rot.z += -Input.GetAxis("Mouse Y") * rotateSpeed;
		rot.z = Mathf.Clamp(rot.z, -maxDown, maxUp);
		cameraTransform.transform.localRotation *= Quaternion.Euler(0, rot.y, 0);
		cameraTransform.GetComponentInChildren<Camera>().transform.localRotation = Quaternion.Euler(rot.z, 0, 0);
	}

	private void Jump()
	{
		if (isGrounded())
		{
			if (isProne)
			{
				//check if can
				if (Physics.SphereCast(transform.position, 0.25f, transform.up, out RaycastHit hitinfo, standingHeight - 0.251f))
				{
					return;
				}
				if (heighTransitionCoroutine != null) StopCoroutine(heighTransitionCoroutine);
				//stop prone
				heighTransitionCoroutine = StartCoroutine(interpolateHeight(col.height, standingHeight, cameraStandingHeight, standToProneTime));
				isProne = false;
				isCrouched = false;
			}
			else if (isCrouched)
			{
				//check if can
				if (Physics.SphereCast(transform.position, 0.25f, transform.up, out RaycastHit hitinfo, standingHeight - 0.251f))
				{
					return;
				}
				if (heighTransitionCoroutine != null) StopCoroutine(heighTransitionCoroutine);
				heighTransitionCoroutine = StartCoroutine(interpolateHeight(col.height, standingHeight, cameraStandingHeight, standToCrouchTime));
				isProne = false;
				isCrouched = false;
			}
			else rb.AddForce(transform.up * jumpForce, ForceMode.Impulse);
		}
	}

	private void Crouch()
	{
		if (heighTransitionCoroutine != null) StopCoroutine(heighTransitionCoroutine);
		
		if (isCrouched)
		{
			//check if can
			if (Physics.SphereCast(transform.position, 0.25f, transform.up, out RaycastHit hitinfo, standingHeight - 0.251f))
			{
				return;
			}
			//stop crouching
			heighTransitionCoroutine = StartCoroutine(interpolateHeight(col.height, standingHeight, cameraStandingHeight, standToCrouchTime));
			isCrouched = false;
		}
		else if (isProne)
		{
			//check if can
			if (Physics.SphereCast(transform.position, 0.25f, transform.up, out RaycastHit hitinfo, crouchingHeight - 0.251f))
			{
				return;
			}
			//start crouching from prone
			heighTransitionCoroutine = StartCoroutine(interpolateHeight(col.height, crouchingHeight, cameraCrouchHeight, crouchToProneTime));
			isProne = false;
			isCrouched = true;
		}
		else
		{
			//start crouching from stand
			heighTransitionCoroutine = StartCoroutine(interpolateHeight(col.height, crouchingHeight, cameraCrouchHeight, standToCrouchTime));
			isCrouched = true;
		}
		
	}

	private void Prone()
	{
		if (heighTransitionCoroutine != null) StopCoroutine(heighTransitionCoroutine);

		if (isProne)
		{
			//check if can
			if (Physics.SphereCast(transform.position, 0.25f, transform.up, out RaycastHit hitinfo, standingHeight - 0.251f))
			{
				return;
			}
			//stop prone
			heighTransitionCoroutine = StartCoroutine(interpolateHeight(col.height, standingHeight, cameraStandingHeight, standToProneTime));
			isProne = false;
		}
		else if (isCrouched)
		{
			//start prone from crouching
			heighTransitionCoroutine = StartCoroutine(interpolateHeight(col.height, proneHeight, cameraProneHeight, crouchToProneTime));
			isCrouched = false;
			isProne = true;
		}
		else
		{
			//start prone from standing
			heighTransitionCoroutine = StartCoroutine(interpolateHeight(col.height, proneHeight, cameraProneHeight, standToProneTime));
			isProne = true;
		}
	}

	private IEnumerator interpolateHeight(float startHeight, float targetHeight, float targetCameraHeight, float timeToTransition)
	{
		float startTime = Time.time;
		float endTime = startTime + timeToTransition;
		float tempCamHeight = currentCameraHeight;

		while (Time.time < endTime)
		{
			float t = (Time.time - startTime) / timeToTransition;
			t = Mathf.Clamp01(t);

			col.height = Mathf.Lerp(startHeight, targetHeight, t);
			col.center = new Vector3(0, col.height / 2, 0);
			currentCameraHeight = Mathf.Lerp(tempCamHeight, targetCameraHeight, t);

			yield return null;
		}

		col.height = targetHeight;
		col.center = new Vector3(0, targetHeight / 2, 0);
		currentCameraHeight = targetCameraHeight;
	}

	private float SpeedControl()
	{
		if (isGrounded())
		{
			if (isProne)
				return proneSpeed;
			else if (isCrouched)
				return crouchSpeed;
			else if (isSprinting)
				return sprintSpeed;
			else if (isWalking)
				return walkSpeed;
			else
				return moveSpeed;
		}
		else return airSpeed;
	}

	private bool isGrounded()
	{
		bool isGrounded = false;
		float distance = transform.up.y - col.radius + 0.015f;
		Ray ray = new Ray(transform.up + transform.position, -transform.up);
		int layerMask = ~LayerMask.GetMask("Player");
		isGrounded = Physics.SphereCast(ray, col.radius - 0.01f, distance, layerMask);

		return isGrounded;
	}

}

#if UNITY_EDITOR
[CustomEditor(typeof(PlayerController))]
public class PlayerControllerEditor : Editor
{

	public override void OnInspectorGUI()
	{
		serializedObject.Update();

		PlayerController controller = (PlayerController)target;

		EditorGUILayout.LabelField("Config", EditorStyles.centeredGreyMiniLabel);
		EditorGUI.indentLevel++;
		controller.showAdditionalFeatures = EditorGUILayout.BeginFoldoutHeaderGroup(controller.showAdditionalFeatures, "Additional Features");
		if (controller.showAdditionalFeatures)
		{
			EditorGUI.indentLevel++;

			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.BeginVertical();

			serializedObject.FindProperty("canRun").boolValue = EditorGUILayout.Toggle("Run", controller.canRun);
			serializedObject.FindProperty("canWalk").boolValue = EditorGUILayout.Toggle("Walk", controller.canWalk);
			serializedObject.FindProperty("canJump").boolValue = EditorGUILayout.Toggle("Jump", controller.canJump);

			EditorGUILayout.EndVertical();

			EditorGUILayout.BeginVertical();

			serializedObject.FindProperty("canCrouch").boolValue = EditorGUILayout.Toggle("Crouch", controller.canCrouch);
			serializedObject.FindProperty("canProne").boolValue = EditorGUILayout.Toggle("Prone", controller.canProne);
			serializedObject.FindProperty("canLean").boolValue = EditorGUILayout.Toggle("Lean", controller.canLean);

			EditorGUILayout.EndVertical();
			EditorGUILayout.EndHorizontal();

			EditorGUI.indentLevel--;
		}
		EditorGUILayout.EndFoldoutHeaderGroup();

		GUILayout.Space(10f);
		EditorGUILayout.LabelField("Default Options", EditorStyles.boldLabel);
		EditorGUI.indentLevel++;
		EditorGUILayout.LabelField("Rotation Options", EditorStyles.boldLabel);
		EditorGUILayout.PropertyField(serializedObject.FindProperty("rotateSpeed"), new GUIContent("Rotate Speed"));
		EditorGUILayout.BeginHorizontal();
		EditorGUILayout.PropertyField(serializedObject.FindProperty("maxUp"), new GUIContent("Max Up"));
		EditorGUILayout.PropertyField(serializedObject.FindProperty("maxDown"), new GUIContent("Max Down"));
		EditorGUILayout.EndHorizontal();
		EditorGUI.indentLevel--;

		
		EditorGUI.indentLevel++;
		EditorGUILayout.LabelField("Movement Options", EditorStyles.boldLabel);
		EditorGUILayout.PropertyField(serializedObject.FindProperty("moveSpeed"), new GUIContent("Move Speed"));
		if(controller.canWalk)
			EditorGUILayout.PropertyField(serializedObject.FindProperty("walkSpeed"), new GUIContent("Walk Speed"));
		if(controller.canRun)
			EditorGUILayout.PropertyField(serializedObject.FindProperty("sprintSpeed"), new GUIContent("Sprint Speed"));
		EditorGUILayout.PropertyField(serializedObject.FindProperty("smoothing"), new GUIContent("Smoothing"));
		EditorGUI.indentLevel--;

		GUILayout.Space(10f);
		if (controller.canCrouch || controller.canJump || controller.canProne || controller.canLean)
			EditorGUILayout.LabelField("Additional Options", EditorStyles.boldLabel);

		if (controller.canJump)
		{
			EditorGUI.indentLevel++;
			EditorGUILayout.LabelField("Jumping Options", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(serializedObject.FindProperty("airSpeed"), new GUIContent("Air Speed"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("jumpForce"), new GUIContent("Jump Force"));
			EditorGUI.indentLevel--;
		}

		if (controller.canCrouch)
		{
			EditorGUI.indentLevel++;
			EditorGUILayout.LabelField("Crouching Options", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(serializedObject.FindProperty("crouchSpeed"), new GUIContent("Crouch Speed"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("crouchingHeight"), new GUIContent("Crouching Height"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("cameraCrouchHeight"), new GUIContent("Camera Crouch Height"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("standToCrouchTime"), new GUIContent("Stand to Crouch Time"));
			EditorGUI.indentLevel--;
		}

		if(controller.canProne)
		{
			EditorGUI.indentLevel++;
			EditorGUILayout.LabelField("Crawling Options", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(serializedObject.FindProperty("proneSpeed"), new GUIContent("Prone Speed"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("proneHeight"), new GUIContent("Prone Height"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("cameraProneHeight"), new GUIContent("Camera Prone Height"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("standToProneTime"), new GUIContent("Stand to Prone Time"));
			EditorGUI.indentLevel--;
		}

		if (controller.canLean)
		{
			EditorGUI.indentLevel++;
			EditorGUILayout.LabelField("Leaning Options", EditorStyles.boldLabel);
			EditorGUI.indentLevel--;
		}
		EditorGUI.indentLevel--;

		serializedObject.ApplyModifiedProperties();
	}
}
#endif
