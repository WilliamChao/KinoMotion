//
// Kino/Motion - Motion blur effect
//
// Copyright (C) 2016 Keijiro Takahashi
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//
using UnityEngine;

namespace Kino
{
    [RequireComponent(typeof(Camera))]
    [AddComponentMenu("Kino Image Effects/Motion")]
    public class Motion : MonoBehaviour
    {
        #region Public enumerations

        /// How the exposure time is determined.
        public enum ExposureTime {
            /// Use Time.deltaTime as the exposure time.
            DeltaTime,
            /// Use a constant time given to shutterSpeed.
            Constant
        }

        /// Amount of sample points.
        public enum SampleCount {
            /// The minimum amount of samples.
            Low,
            /// A medium amount of samples. Recommended for typical use.
            Medium,
            /// A large amount of samples.
            High,
            /// Use a given number of samples (customSampleCount)
            Custom
        }

        #endregion

        #region Public properties

        /// How the exposure time is determined.
        public ExposureTime exposureTime {
            get { return _exposureTime; }
            set { _exposureTime = value; }
        }

        [SerializeField]
        [Tooltip("How the exposure time is determined.")]
        ExposureTime _exposureTime = ExposureTime.DeltaTime;

        /// The angle of rotary shutter. The larger the angle is, the longer
        /// the exposure time is. This value is only used in delta time mode.
        public float shutterAngle {
            get { return _shutterAngle; }
            set { _shutterAngle = value; }
        }

        [SerializeField, Range(0, 360)]
        [Tooltip("The angle of rotary shutter. Larger values give longer exposure.")]
        float _shutterAngle = 270;

        /// The denominator of the custom shutter speed. This value is only
        /// used in constant time mode.
        public int shutterSpeed {
            get { return _shutterSpeed; }
            set { _shutterSpeed = value; }
        }

        [SerializeField]
        [Tooltip("The denominator of the shutter speed.")]
        int _shutterSpeed = 48;

        /// The amount of sample points, which affects quality and performance.
        public SampleCount sampleCount {
            get { return _sampleCount; }
            set { _sampleCount = value; }
        }

        [SerializeField]
        [Tooltip("The amount of sample points, which affects quality and performance.")]
        SampleCount _sampleCount = SampleCount.Medium;

        /// The number of sample points. This value is only used when
        /// SampleCount.Custom is given to sampleCount.
        public int customSampleCount {
            get { return _customSampleCount; }
            set { _customSampleCount = value; }
        }

        [SerializeField]
        int _customSampleCount = 10;

        /// The maximum length of motion blur, given as a percentage of the
        /// screen height. The larger the value is, the stronger the effects
        /// are, but also the more noticeable artifacts it gets.
        public float maxBlurRadius {
            get { return Mathf.Clamp(_maxBlurRadius, 0.5f, 10.0f); }
            set { _maxBlurRadius = value; }
        }

        [SerializeField, Range(0.5f, 10.0f)]
        [Tooltip("The maximum length of motion blur, given as a percentage " +
         "of the screen height. Larger values may introduce artifacts.")]
        float _maxBlurRadius = 5.0f;

        // Color accumulation ratio.
        public float accumulationRatio {
            get { return _accumulationRatio; }
            set { _accumulationRatio = value; }
        }

        [SerializeField, Range(0.0f, 0.99f)]
        [Tooltip("Color accumulation ratio.")]
        float _accumulationRatio = 0;

        #endregion

        #region Debug settings

        enum DebugMode { Off, Velocity, NeighborMax, Depth }

        [SerializeField]
        [Tooltip("The debug visualization mode.")]
        DebugMode _debugMode;

        #endregion

        #region Private properties and methods

        [SerializeField] Shader _shader;

        Material _material;
        RenderTexture _accTexture;
        int _previousFrameCount;

        float VelocityScale {
            get {
                if (exposureTime == ExposureTime.Constant)
                    return 1.0f / (shutterSpeed * Time.smoothDeltaTime);
                else // ExposureTime.DeltaTime
                    return Mathf.Clamp01(shutterAngle / 360);
            }
        }

        int LoopCount {
            get {
                switch (_sampleCount)
                {
                    case SampleCount.Low:    return 2;  // 4 samples
                    case SampleCount.Medium: return 5;  // 10 samples
                    case SampleCount.High:   return 10; // 20 samples
                }
                // SampleCount.Custom
                return Mathf.Clamp(_customSampleCount / 2, 1, 64);
            }
        }

        RenderTexture GetTemporaryRT(Texture source, int divider, RenderTextureFormat format)
        {
            var w = source.width / divider;
            var h = source.height / divider;
            var rt = RenderTexture.GetTemporary(w, h, 0, format);
            rt.filterMode = FilterMode.Point;
            return rt;
        }

        void ReleaseTemporaryRT(RenderTexture rt)
        {
            RenderTexture.ReleaseTemporary(rt);
        }

        #endregion

        #region MonoBehaviour functions

        void OnEnable()
        {
            _material = new Material(Shader.Find("Hidden/Kino/Motion"));
            _material.hideFlags = HideFlags.DontSave;

            GetComponent<Camera>().depthTextureMode |=
                DepthTextureMode.Depth | DepthTextureMode.MotionVectors;
        }

        void OnDisable()
        {
            DestroyImmediate(_material);
            _material = null;

            if (_accTexture != null) ReleaseTemporaryRT(_accTexture);
            _accTexture = null;
        }

        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            // Texture format for storing packed velocity/depth.
            const RenderTextureFormat packedRTFormat = RenderTextureFormat.ARGB2101010;

            // Texture format for storing 2D vectors.
            const RenderTextureFormat vectorRTFormat = RenderTextureFormat.RGHalf;

            // Calculate the maximum blur radius in pixels.
            var maxBlurPixels = (int)(maxBlurRadius * source.height / 100);

            // Calculate the TileMax size.
            // It should be a multiple of 8 and larger than maxBlur.
            var tileSize = ((maxBlurPixels - 1) / 8 + 1) * 8;

            // Pass 1 - Velocity/depth packing
            _material.SetFloat("_VelocityScale", VelocityScale);
            _material.SetFloat("_MaxBlurRadius", maxBlurPixels);

            var vbuffer = GetTemporaryRT(source, 1, packedRTFormat);
            Graphics.Blit(null, vbuffer, _material, 0);

            // Pass 2 - First TileMax filter (1/4 downsize)
            var tile4 = GetTemporaryRT(source, 4, vectorRTFormat);
            Graphics.Blit(vbuffer, tile4, _material, 1);

            // Pass 3 - Second TileMax filter (1/2 downsize)
            var tile8 = GetTemporaryRT(source, 8, vectorRTFormat);
            Graphics.Blit(tile4, tile8, _material, 2);
            ReleaseTemporaryRT(tile4);

            // Pass 4 - Third TileMax filter (reduce to tileSize)
            var tileMaxOffs = Vector2.one * (tileSize / 8.0f - 1) * -0.5f;
            _material.SetVector("_TileMaxOffs", tileMaxOffs);
            _material.SetInt("_TileMaxLoop", tileSize / 8);

            var tile = GetTemporaryRT(source, tileSize, vectorRTFormat);
            Graphics.Blit(tile8, tile, _material, 3);
            ReleaseTemporaryRT(tile8);

            // Pass 5 - NeighborMax filter
            var neighborMax = GetTemporaryRT(source, tileSize, vectorRTFormat);
            Graphics.Blit(tile, neighborMax, _material, 4);
            ReleaseTemporaryRT(tile);

            // Pass 6 - Reconstruction pass
            _material.SetInt("_LoopCount", LoopCount);
            _material.SetFloat("_MaxBlurRadius", maxBlurPixels);
            _material.SetTexture("_NeighborMaxTex", neighborMax);
            _material.SetTexture("_VelocityTex", vbuffer);
            _material.SetTexture("_AccTex", _accTexture);
            _material.SetFloat("_AccRatio", _accumulationRatio);

            if (_debugMode != DebugMode.Off)
            {
                // Debug mode: Blit with the debug shader.
                Graphics.Blit(source, destination, _material, 6 + (int)_debugMode);
            }
            else if (_accumulationRatio == 0)
            {
                // Reconstruction without color accumulation
                Graphics.Blit(source, destination, _material, 5);

                // Accumulation texture is not needed now.
                if (_accTexture != null)
                {
                    ReleaseTemporaryRT(_accTexture);
                    _accTexture = null;
                }
            }
            else
            {
                // Reconstruction with color accumulation
                Graphics.Blit(source, destination, _material, 6);

                // Accumulation only happens when time advances.
                if (Time.frameCount != _previousFrameCount)
                {
                    // Release the accumulation texture when accumulation is
                    // disabled or the size of the screen was changed.
                    if (_accTexture != null &&
                        (_accTexture.width != source.width ||
                         _accTexture.height != source.height))
                    {
                        ReleaseTemporaryRT(_accTexture);
                        _accTexture = null;
                    }

                    // Create an accumulation texture if not ready.
                    if (_accTexture == null)
                        _accTexture = GetTemporaryRT(source, 1, source.format);

                    Graphics.Blit(destination, _accTexture);
                    _previousFrameCount = Time.frameCount;
                }
            }

            // Cleaning up
            ReleaseTemporaryRT(vbuffer);
            ReleaseTemporaryRT(neighborMax);
        }

        #endregion
    }
}
