﻿using UnityEngine;
using System.Collections;
using UnityEngine.Assertions;
using UnityEngine.Events;


namespace Prime31
{
	[RequireComponent( typeof( SpriteRenderer ) )]
	public class SpriteAnimator : MonoBehaviour
	{
		[System.Serializable]
		public class AnimationTrigger
		{
			[System.Serializable]
			public class AnimationEvent : UnityEvent<int> {}

			public int frame;
			public AnimationEvent onEnteredFrame;
		}


		[System.Serializable]
		public class Animation
		{
			public string name;
			public float fps = 5;
			public bool loop;
			public bool pingPong;
			public float delay = 0f;
			public Sprite[] frames;
			public AnimationTrigger[] triggers;

			[System.NonSerialized][HideInInspector]
			public float secondsPerFrame;
			[System.NonSerialized][HideInInspector]
			public float iterationDuration;
			[System.NonSerialized][HideInInspector]
			public float totalDuration;


			public void prepareForUse()
			{
				secondsPerFrame = 1f / fps;
				iterationDuration = secondsPerFrame * (float)frames.Length;

				if( loop )
					totalDuration = Mathf.Infinity;
				else if( pingPong )
					totalDuration = iterationDuration * 2f;
				else
					totalDuration = iterationDuration;
			}
		}


		public Animation[] animations;
		public bool isPlaying { get; private set; }
		public int currentFrame { get; private set; }
		[HideInInspector]
		[SerializeField]
		public string playAnimationOnStart;

		Transform _transform;
		Animation _currentAnimation;
		SpriteRenderer _spriteRenderer;

		float _totalElapsedTime;
		float _elapsedDelay;
		int _completedIterations;
		bool _delayComplete;
		bool _isReversed;
		bool _isLoopingBackOnPingPong;



		#region MonoBehavior

		void Awake()
		{
			_spriteRenderer = GetComponent<SpriteRenderer>();
			_transform = gameObject.transform;

			if( playAnimationOnStart != string.Empty )
				play( playAnimationOnStart );
		}


		void OnDisable()
		{
			isPlaying = false;
			_currentAnimation = null;
		}


		void Update()
		{
			if( _currentAnimation == null || !isPlaying )
				return;

			// handle delay
			if( !_delayComplete && _elapsedDelay < _currentAnimation.delay )
			{
				_elapsedDelay += Time.deltaTime;
				if( _elapsedDelay >= _currentAnimation.delay )
					_delayComplete = true;
				
				return;
			}

			// count backwards if we are going in reverse
			if( _isReversed )
				_totalElapsedTime -= Time.deltaTime;
			else
				_totalElapsedTime += Time.deltaTime;


			_totalElapsedTime = Mathf.Clamp( _totalElapsedTime, 0f, _currentAnimation.totalDuration );
			_completedIterations = Mathf.FloorToInt( _totalElapsedTime / _currentAnimation.iterationDuration );
			_isLoopingBackOnPingPong = false;


			// handle ping pong loops. if loop is false but pingPongLoop is true we allow a single forward-then-backward iteration
			if( _currentAnimation.pingPong )
			{
				if( _currentAnimation.loop || _completedIterations < 2 )
					_isLoopingBackOnPingPong = _completedIterations % 2 != 0;
			}


			var elapsedTime = 0f;
			if( _totalElapsedTime < _currentAnimation.iterationDuration )
			{
				elapsedTime = _totalElapsedTime;
			}
			else
			{
				elapsedTime = _totalElapsedTime % _currentAnimation.iterationDuration;
			}


			// if we reversed the animation and we reached 0 total elapsed time handle un-reversing things and loop continuation
			if( _isReversed && _totalElapsedTime <= 0 )
			{
				_isReversed = false;

				if( _currentAnimation.loop )
				{
					_totalElapsedTime = 0f;
				}
				else
				{
					isPlaying = false;
					return;
				}
			}


			// time goes backwards when we are reversing a ping-pong loop
			if( _isLoopingBackOnPingPong )
				elapsedTime = _currentAnimation.iterationDuration - elapsedTime;


			// fetch our desired frame
			var desiredFrame = Mathf.FloorToInt( elapsedTime / _currentAnimation.secondsPerFrame );
			if( desiredFrame != currentFrame )
			{
				currentFrame = desiredFrame;
				_spriteRenderer.sprite = _currentAnimation.frames[currentFrame];
				handleFrameChanged();

				// ping-pong needs special care. we don't want to double the frame time when wrapping so we man-handle the totalElapsedTime
				if( _currentAnimation.pingPong && ( currentFrame == 0 || currentFrame == _currentAnimation.frames.Length - 1 ) )
				{
					if( _isReversed )
						_totalElapsedTime -= _currentAnimation.secondsPerFrame;
					else
						_totalElapsedTime += _currentAnimation.secondsPerFrame;
				}
			}
		}

		#endregion


		#region Playback control

		public void play( string name, int startFrame = 0 )
		{
			var animation = getAnimation( name );
			if( animation != null )
			{
				animation.prepareForUse();

				_currentAnimation = animation;
				isPlaying = true;
				_isReversed = false;
				currentFrame = startFrame;
				_spriteRenderer.sprite = animation.frames[currentFrame];

				_totalElapsedTime = (float)startFrame * _currentAnimation.secondsPerFrame;
			}
		}


		public bool isAnimationPlaying( string name )
		{
			return ( _currentAnimation != null && _currentAnimation.name == name );
		}


		public void pause()
		{
			isPlaying = false;
		}


		public void unPause()
		{
			isPlaying = true;
		}


		public void reverseAnimation()
		{
			_isReversed = !_isReversed;
		}


		public void stop()
		{
			isPlaying = false;
			_spriteRenderer.sprite = null;
			_currentAnimation = null;
		}

		#endregion


		Animation getAnimation( string name )
		{
			for( var i = 0; i < animations.Length; i++ )
			{
				if( animations[i].name == name )
					return animations[i];
			}

			Debug.LogError( "Animation [" + name + "] does not exist" );

			return null;
		}


		void handleFrameChanged()
		{
			for( var i = 0; i < _currentAnimation.triggers.Length; i++ )
			{
				if( _currentAnimation.triggers[i].frame == currentFrame && _currentAnimation.triggers[i].onEnteredFrame != null )
					_currentAnimation.triggers[i].onEnteredFrame.Invoke( currentFrame );
			}
		}


		public int getFacing()
		{
			return (int)Mathf.Sign( _transform.localScale.x );
		}


		public void flip()
		{
			var scale = _transform.localScale;
			scale.x *= -1f;
			_transform.localScale = scale;
		}


		public void faceLeft()
		{
			var scale = _transform.localScale;
			if( scale.x > 0f )
			{
				scale.x *= -1f;
				_transform.localScale = scale;
			}
		}


		public void faceRight()
		{
			var scale = _transform.localScale;
			if( scale.x < 0f )
			{
				scale.x *= -1f;
				_transform.localScale = scale;
			}
		}
	
	}
}