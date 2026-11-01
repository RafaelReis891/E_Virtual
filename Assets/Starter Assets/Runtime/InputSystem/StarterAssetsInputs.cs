using UnityEngine;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

#endif

namespace StarterAssets
{
	public class StarterAssetsInputs : MonoBehaviour
	{
		[Header("Character Input Values")]
		public Vector2 move;
		public Vector2 look;
		public bool jump;
		public bool sprint;

		[Header("Movement Settings")]
		public bool analogMovement;

		[Header("Mouse Cursor Settings")]
		public bool cursorLocked = true;
		public bool cursorInputForLook = true;

		/// <summary>Site pause. Mouse look must not rotate the camera while this is set.</summary>
		public static bool GamePaused { get; set; }

#if ENABLE_INPUT_SYSTEM
		public void OnMove(InputValue value)
		{
			MoveInput(value.Get<Vector2>());
		}

		public void OnLook(InputValue value)
		{
			if (GamePaused || !cursorInputForLook)
			{
				LookInput(Vector2.zero);
				return;
			}
			LookInput(value.Get<Vector2>());
		}

		public void OnJump(InputValue value)
		{
			JumpInput(value.isPressed);
		}

		public void OnSprint(InputValue value)
		{
			SprintInput(value.isPressed);
		}
#endif


		public void MoveInput(Vector2 newMoveDirection)
		{
			move = newMoveDirection;
		} 

		public void LookInput(Vector2 newLookDirection)
		{
			look = GamePaused ? Vector2.zero : newLookDirection;
		}

		public void JumpInput(bool newJumpState)
		{
			jump = newJumpState;
		}

		public void SprintInput(bool newSprintState)
		{
			sprint = newSprintState;
		}
		
		private void OnApplicationFocus(bool hasFocus)
		{
			if (GamePaused || cursorForcedUnlock) return;
			SetCursorState(cursorLocked);
		}

		private void SetCursorState(bool newState)
		{
			Cursor.lockState = newState ? CursorLockMode.Locked : CursorLockMode.None;
		}

		bool cursorForcedUnlock;
		public void UnlockCursor(bool newState)
		{
			Cursor.lockState = newState ? CursorLockMode.None : CursorLockMode.Locked;
			cursorForcedUnlock = newState;
		}

		private void Update()
		{
			if (GamePaused) return;
			if(Mouse.current.rightButton.wasPressedThisFrame)
			{
				UnlockCursor(!cursorForcedUnlock);
			}
		}
	}
	
}