using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace AraJob
{
    public static class UnitySrcAssist
    {

        public static Color GradientEvaluate(NativeList<GradientColorKey> rGradientColorKeys, NativeList<GradientAlphaKey> rGradientAlphaKeys, GradientMode rMode, float fTime)
        {
            //rGradientColorKeys length 2-8
            //rGradientAlphaKeys length 2-8
            Color nColor = Color.white;
            float fAlpha = 1f;
            fTime = Mathf.Clamp(fTime, 0f, 1f);

            switch (rMode)
            {
                case GradientMode.Blend:

                    //Color Blend
                    if(fTime <= rGradientColorKeys[0].time)
                    {
                        nColor = rGradientColorKeys[0].color;
                    }
                    else if (fTime >= rGradientColorKeys[rGradientColorKeys.Length - 1].time)
                    {
                        nColor = rGradientColorKeys[rGradientColorKeys.Length - 1].color;
                    }
                    else
                    {
                        for (int i = 0; i < rGradientColorKeys.Length; i++)
                        {
                            if (fTime == rGradientColorKeys[i].time)
                            {
                                nColor = rGradientColorKeys[i].color;
                                break;
                            }
                            else if (fTime < rGradientColorKeys[i].time)
                            {

                                Color preColor = rGradientColorKeys[i - 1].color;
                                Color nextColor = rGradientColorKeys[i].color;

                                float length = rGradientColorKeys[i].time - rGradientColorKeys[i - 1].time;
                                nColor = Color.Lerp(preColor, nextColor, (fTime - rGradientColorKeys[i - 1].time) / length);
                                break;
                            }
                        }
                    }


                    //Alpha Blend
                    if (fTime <= rGradientAlphaKeys[0].time)
                    {
                        fAlpha = rGradientAlphaKeys[0].alpha;
                    }
                    else if(fTime >= rGradientAlphaKeys[rGradientAlphaKeys.Length - 1].time)
                    {
                        fAlpha = rGradientAlphaKeys[rGradientAlphaKeys.Length - 1].alpha;
                    }
                    else
                    {
                        for (int i = 0; i < rGradientAlphaKeys.Length; i++)
                        {
                            if (fTime == rGradientAlphaKeys[i].time)
                            {
                                fAlpha = rGradientAlphaKeys[i].alpha;
                                break;
                            }
                            else if (fTime < rGradientAlphaKeys[i].time)
                            {
                                float preAlpha = rGradientAlphaKeys[i - 1].alpha;
                                float nextAlpha = rGradientAlphaKeys[i].alpha;

                                float length = rGradientAlphaKeys[i].time - rGradientAlphaKeys[i - 1].time;
                                fAlpha = Mathf.Lerp(preAlpha, nextAlpha, (fTime - rGradientAlphaKeys[i - 1].time) / length);
                                break;
                            }
                        }
                    }

                    break;
                case GradientMode.Fixed:

                    //Color Blend
                    if (fTime <= rGradientColorKeys[0].time)
                    {
                        nColor = rGradientColorKeys[0].color;
                    }
                    else if (fTime >= rGradientColorKeys[rGradientColorKeys.Length - 1].time)
                    {
                        nColor = rGradientColorKeys[rGradientColorKeys.Length - 1].color;
                    }
                    else
                    {
                        for (int i = 0; i < rGradientColorKeys.Length; i++)
                        {
                            if (fTime <= rGradientColorKeys[i].time)
                            {
                                nColor = rGradientColorKeys[i].color;
                                break;
                            }
                        }
                    }



                    //Alpha Blend
                    if (fTime <= rGradientAlphaKeys[0].time)
                    {
                        fAlpha = rGradientAlphaKeys[0].alpha;
                    }
                    else if (fTime >= rGradientAlphaKeys[rGradientAlphaKeys.Length - 1].time)
                    {
                        fAlpha = rGradientAlphaKeys[rGradientAlphaKeys.Length - 1].alpha;
                    }
                    else
                    {
                        for (int i = 0; i < rGradientAlphaKeys.Length; i++)
                        {
                            if (fTime <= rGradientAlphaKeys[i].time)
                            {
                                fAlpha = rGradientAlphaKeys[i].alpha;
                                break;
                            }
                        }
                    }

                    break;
                default:
                    break;
            }

            nColor.a = fAlpha;
            return nColor;
        }

        public static float AnimationCurveEvaluate(NativeList<Keyframe> rKeyframes, float fTime)
        {
            float fValue;

            int keyNumber = rKeyframes.Length;
            if(keyNumber == 1)
            {
                fValue = rKeyframes[0].value;
            }
            else
            {
                Keyframe startPoint, endPoint;
                startPoint = rKeyframes[0];
                endPoint = rKeyframes[keyNumber - 1];

                if (fTime <= startPoint.time)
                    fValue = startPoint.value;
                else if (fTime >= endPoint.time)
                    fValue = endPoint.value;
                else
                {
                    //找到相邻左右临界点
                    Keyframe prePoint, nextPoint;
                    prePoint = startPoint;
                    nextPoint = endPoint;

                    for (int i = 0; i < keyNumber; i++)
                    {
                        if(fTime >= rKeyframes[i].time && fTime < rKeyframes[i + 1].time)
                        {
                            prePoint = rKeyframes[i];
                            nextPoint = rKeyframes[i + 1];
                            break;
                        }
                    }

                    ////计算Hermite曲线参数  Mh   Gh => K = [K0  K1  K2 K4]
                    //float K0, K1, K2, K3;
                    //float dx = nextPoint.time - prePoint.time;
                    //dx = Math.Max(dx, 0.0001f);
                    //float dy = nextPoint.value - prePoint.value;
                    //float length = 1.0f / (dx * dx);

                    //float R0 = prePoint.outTangent;
                    //float R1 = nextPoint.inTangent;
                    //float d1 = R0 * dx;
                    //float d2 = R1 * dx;

                    //K0 = (d1 + d2 - dy - dy) * length / dx;
                    //K1 = (dy + dy + dy - d1 - d1 - d2) * length;
                    //K2 = R0;
                    //K3 = prePoint.value;

                    ////计算t
                    //float t = fTime - prePoint.time;
                    ////代入Hermite曲线公式
                    //fValue = (t * (t * (t * K0) + K1) + K2) + K3;

                    if (math.isinf(prePoint.outTangent) || math.isinf(nextPoint.inTangent))
                    {
                        return prePoint.value;
                    }


                    //return (float)AnimationCurveInterpolant(prePoint.time, prePoint.value, prePoint.outTangent, prePoint.outWeight, 
                    //                                 nextPoint.time, nextPoint.value, nextPoint.inTangent, nextPoint.inWeight,
                    //                                 fTime);

                    //if (prePoint.outWeight == 0)
                    //    prePoint.outWeight = 1.0f / 3.0f;
                    //if (nextPoint.inWeight == 0)
                    //    nextPoint.inWeight = 1.0f / 3.0f;

                    return HermiteInterpolate(prePoint, nextPoint, fTime);
                    //fValue = HermiteInterpolate(prePoint, nextPoint, fTime);
                    //fValue = BezierInterpolate(prePoint, nextPoint, fTime);

                    //if (prePoint.outWeight == 1.0f / 3.0f && nextPoint.inWeight == 1.0f / 3.0f)
                    //{
                    //    return HermiteInterpolate(prePoint, nextPoint, fTime);
                    //}
                    //return BezierInterpolate(prePoint, nextPoint, fTime);
                }
            }
            return fValue;
        }


        #region AnimationCurve Methods

        static float HermiteInterpolate(Keyframe prePoint, Keyframe nextPoint, float fTime)
        {
            float dx = nextPoint.time - prePoint.time;
            float m0;
            float m1;
            float t;
            if (dx != 0.0f)
            {
                t = (fTime - prePoint.time) / dx;
                m0 = prePoint.outTangent * dx;
                m1 = nextPoint.inTangent * dx;
            }
            else
            {
                t = 0.0f;
                m0 = 0;
                m1 = 0;
            }

            return HermiteInterpolate(t, prePoint.value, m0, m1, nextPoint.value);
        }

        static float HermiteInterpolate(float t, float p0, float m0, float m1, float p1)
        {
            // Unrolled the equations to avoid precision issue.
            // (2 * t^3 -3 * t^2 +1) * p0 + (t^3 - 2 * t^2 + t) * m0 + (-2 * t^3 + 3 * t^2) * p1 + (t^3 - t^2) * m1

            var a = 2.0f * p0 + m0 - 2.0f * p1 + m1;
            var b = -3.0f * p0 - 2.0f * m0 + 3.0f * p1 - m1;
            var c = m0;
            var d = p0;

            return t * (t * (a * t + b) + c) + d;
        }

        static float FAST_CBRT_POSITIVE(float x)
        {
            return math.exp(math.log(x) / 3.0f);
        }

        static float FAST_CBRT(float x)
        {
            return (((x) < 0) ? -math.exp(math.log(-(x)) / 3.0f) : math.exp(math.log(x) / 3.0f));
        }

        static float BezierInterpolate(Keyframe prePoint, Keyframe nextPoint, float curveT)
        {
            float lhsOutWeight = prePoint.outWeight;
            float rhsInWeight = nextPoint.inWeight;

            float dx = nextPoint.time - prePoint.time;
            if (dx == 0.0F)
                return prePoint.value;

            return BezierInterpolate((curveT - prePoint.time) / dx, prePoint.value, prePoint.outWeight * dx, lhsOutWeight, nextPoint.value, nextPoint.inTangent * dx, rhsInWeight);
        }

        static float BezierInterpolate(float t, float v1, float m1, float w1, float v2, float m2, float w2)
        {
            float u = BezierExtractU(t, w1, 1.0F - w2);
            return BezierInterpolate(u, v1, w1 * m1 + v1, v2 - w2 * m2, v2);
        }

        static float BezierExtractU(float t, float w1, float w2)
        {
            float a = 3.0F * w1 - 3.0F * w2 + 1.0F;
            float b = -6.0F * w1 + 3.0F * w2;
            float c = 3.0F * w1;
            float d = -t;

            if (math.abs(a) > 1e-3f)
            {
                float p = -b / (3.0F * a);
                float p2 = p * p;
                float p3 = p2 * p;

                float q = p3 + (b * c - 3.0F * a * d) / (6.0F * a * a);
                float q2 = q * q;

                float r = c / (3.0F * a);
                float rmp2 = r - p2;

                float s = q2 + rmp2 * rmp2 * rmp2;

                if (s < 0.0F)
                {
                    float ssi = math.sqrt(-s);
                    float r_1 = math.sqrt(-s + q2);
                    float phi = math.atan2(ssi, q);

                    float r_3 = FAST_CBRT_POSITIVE(r_1);
                    float phi_3 = phi / 3.0F;

                    // Extract cubic roots.
                    float u1 = 2.0F * r_3 * math.cos(phi_3) + p;
                    float u2 = 2.0F * r_3 * math.cos(phi_3 + 2.0F * (float)math.PI / 3.0f) + p;
                    float u3 = 2.0F * r_3 * math.cos(phi_3 - 2.0F * (float)math.PI / 3.0f) + p;

                    if (u1 >= 0.0F && u1 <= 1.0F)
                        return u1;
                    else if (u2 >= 0.0F && u2 <= 1.0F)
                        return u2;
                    else if (u3 >= 0.0F && u3 <= 1.0F)
                        return u3;

                    // Aiming at solving numerical imprecisions when u is outside [0,1].
                    return (t < 0.5F) ? 0.0F : 1.0F;
                }
                else
                {
                    float ss = math.sqrt(s);
                    float u = FAST_CBRT(q + ss) + FAST_CBRT(q - ss) + p;

                    if (u >= 0.0F && u <= 1.0F)
                        return u;

                    // Aiming at solving numerical imprecisions when u is outside [0,1].
                    return (t < 0.5F) ? 0.0F : 1.0F;
                }
            }

            if (math.abs(b) > 1e-3f)
            {
                float s = c * c - 4.0F * b * d;
                float ss = math.sqrt(s);

                float u1 = (-c - ss) / (2.0F * b);
                float u2 = (-c + ss) / (2.0F * b);

                if (u1 >= 0.0F && u1 <= 1.0F)
                    return u1;
                else if (u2 >= 0.0F && u2 <= 1.0F)
                    return u2;

                // Aiming at solving numerical imprecisions when u is outside [0,1].
                return (t < 0.5F) ? 0.0F : 1.0F;
            }

            if (math.abs(c) > 1e-3f)
            {
                return (-d / c);
            }

            return 0.0F;
        }

        static float BezierInterpolate(float t, float p0, float p1, float p2, float p3)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            float omt = 1.0F - t;
            float omt2 = omt * omt;
            float omt3 = omt2 * omt;

            return omt3 * p0 + 3.0F * t * omt2 * p1 + 3.0F * t2 * omt * p2 + t3 * p3;
        }


        //public const double Eps = 2.22e-16;

        public static double AnimationCurveInterpolant(double x1, double y1, double yp1, double wt1, double x2, double y2, double yp2, double wt2, double x)
        {
            double dx = x2 - x1;
            x = (x - x1) / dx;
            double dy = y2 - y1;
            yp1 = yp1 * dx / dy;
            yp2 = yp2 * dx / dy;
            double wt2s = 1 - wt2;

            double t = 0.5;
            double t2;

            if (wt1 == 1 / 3.0 && wt2 == 1 / 3.0)
            {
                t = x;
                t2 = 1 - t;
            }
            else
            {
                while (true)
                {
                    t2 = (1 - t);
                    double fg = 3 * t2 * t2 * t * wt1 + 3 * t2 * t * t * wt2s + t * t * t - x;
                    if (Math.Abs(fg) < 2 * 2.22e-16)
                        break;

                    // third order householder method
                    double fpg = 3 * t2 * t2 * wt1 + 6 * t2 * t * (wt2s - wt1) + 3 * t * t * (1 - wt2s);
                    double fppg = 6 * t2 * (wt2s - 2 * wt1) + 6 * t * (1 - 2 * wt2s + wt1);
                    double fpppg = 18 * wt1 - 18 * wt2s + 6;

                    t -= (6 * fg * fpg * fpg - 3 * fg * fg * fppg) / (6 * fpg * fpg * fpg - 6 * fg * fpg * fppg + fg * fg * fpppg);
                }
            }

            double y = 3 * t2 * t2 * t * wt1 * yp1 + 3 * t2 * t * t * (1 - wt2 * yp2) + t * t * t;

            return y * dy + y1;
        }
        #endregion


    }
}
