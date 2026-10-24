namespace NotchMusic.Helpers;

/// <summary>
/// Menyediakan script DSP Web Audio API untuk menghilangkan suara vokal (Center-Channel Vocal Cancellation / Karaoke Mode)
/// secara real-time dengan tetap mempertahankan frekuensi Bass &amp; Kick Drum (&lt; 165 Hz).
/// </summary>
public static class WebAudioVocalRemoverScript
{
    public static string GetBootstrapScript(bool initialEnabled = false)
    {
        var enabledLiteral = initialEnabled ? "true" : "false";
        return $$"""
            (function() {
                if (!window.__dranotchKaraoke) {
                    window.__dranotchKaraoke = {
                        enabled: {{enabledLiteral}},
                        ctx: null,
                        nodes: new WeakMap(),
                        trackedElements: [],
                        ensureContext: function() {
                            if (!this.ctx) {
                                const AudioCtx = window.AudioContext || window.webkitAudioContext;
                                this.ctx = new AudioCtx();
                            }
                            if (this.ctx.state === 'suspended') {
                                this.ctx.resume().catch(function() {});
                            }
                            return this.ctx;
                        },
                        attachToElement: function(el) {
                            if (!el) return null;
                            if (this.nodes.has(el)) return this.nodes.get(el);
                            try {
                                const ctx = this.ensureContext();
                                const source = ctx.createMediaElementSource(el);

                                // 1. Jalur Normal (Bypass)
                                const normalGain = ctx.createGain();
                                normalGain.gain.value = this.enabled ? 0.0 : 1.0;
                                source.connect(normalGain);
                                normalGain.connect(ctx.destination);

                                // 2. Jalur Karaoke (Vocal Remover DSP)
                                const karaokeOut = ctx.createGain();
                                karaokeOut.gain.value = this.enabled ? 1.0 : 0.0;

                                const compressor = ctx.createDynamicsCompressor();
                                compressor.threshold.value = -6;
                                compressor.knee.value = 12;
                                compressor.ratio.value = 4;
                                compressor.attack.value = 0.003;
                                compressor.release.value = 0.15;

                                // 2a. Mid-Side Phase Cancellation (L - R) untuk frekuensi vokal (> 165 Hz)
                                const splitter = ctx.createChannelSplitter(2);
                                const invertRight = ctx.createGain();
                                invertRight.gain.value = -1.0;

                                const diffSum = ctx.createGain();
                                diffSum.gain.value = 1.28;

                                const highPass = ctx.createBiquadFilter();
                                highPass.type = 'highpass';
                                highPass.frequency.value = 165;
                                highPass.Q.value = 0.707;

                                const midWarmth = ctx.createBiquadFilter();
                                midWarmth.type = 'peaking';
                                midWarmth.frequency.value = 1100;
                                midWarmth.Q.value = 0.8;
                                midWarmth.gain.value = -2.0;

                                const merger = ctx.createChannelMerger(2);

                                source.connect(splitter);
                                splitter.connect(diffSum, 0);
                                splitter.connect(invertRight, 1);
                                invertRight.connect(diffSum);

                                diffSum.connect(highPass);
                                highPass.connect(midWarmth);
                                midWarmth.connect(merger, 0, 0);
                                midWarmth.connect(merger, 0, 1);
                                merger.connect(compressor);

                                // 2b. Pelestarian Bass & Kick Drum (< 165 Hz) agar musik tetap bulat
                                const bassLowPass = ctx.createBiquadFilter();
                                bassLowPass.type = 'lowpass';
                                bassLowPass.frequency.value = 165;
                                bassLowPass.Q.value = 0.707;

                                const bassGain = ctx.createGain();
                                bassGain.gain.value = 0.95;

                                source.connect(bassLowPass);
                                bassLowPass.connect(bassGain);
                                bassGain.connect(compressor);

                                compressor.connect(karaokeOut);
                                karaokeOut.connect(ctx.destination);

                                const entry = { el: el, normalGain: normalGain, karaokeOut: karaokeOut };
                                this.nodes.set(el, entry);
                                this.trackedElements.push(entry);
                                return entry;
                            } catch (err) {
                                console.warn('DRANOTCH Karaoke DSP attach warning:', err);
                                return null;
                            }
                        },
                        applyAll: function() {
                            const mediaEls = document.querySelectorAll('video, audio');
                            if (!this.enabled && this.trackedElements.length === 0) {
                                return;
                            }
                            const ctx = this.ensureContext();
                            const now = ctx.currentTime;
                            const targetNormal = this.enabled ? 0.0 : 1.0;
                            const targetKaraoke = this.enabled ? 1.0 : 0.0;
                            const self = this;
                            mediaEls.forEach(function(el) {
                                const entry = self.attachToElement(el);
                                if (entry) {
                                    entry.normalGain.gain.setTargetAtTime(targetNormal, now, 0.025);
                                    entry.karaokeOut.gain.setTargetAtTime(targetKaraoke, now, 0.025);
                                }
                            });
                        },
                        setEnabled: function(flag) {
                            this.enabled = !!flag;
                            this.applyAll();
                        }
                    };

                    document.addEventListener('play', function() {
                        if (window.__dranotchKaraoke && window.__dranotchKaraoke.enabled) {
                            window.__dranotchKaraoke.applyAll();
                        }
                    }, true);

                    document.addEventListener('playing', function() {
                        if (window.__dranotchKaraoke && window.__dranotchKaraoke.enabled) {
                            window.__dranotchKaraoke.applyAll();
                        }
                    }, true);

                    setInterval(function() {
                        if (window.__dranotchKaraoke && window.__dranotchKaraoke.enabled) {
                            window.__dranotchKaraoke.applyAll();
                        }
                    }, 900);
                }
                if ({{enabledLiteral}}) {
                    window.__dranotchKaraoke.setEnabled(true);
                }
            })();
            """;
    }

    public static string GetSetEnabledScript(bool enabled)
    {
        var enabledLiteral = enabled ? "true" : "false";
        return $$"""
            (function() {
                {{GetBootstrapScript(enabled)}}
                if (window.__dranotchKaraoke) {
                    window.__dranotchKaraoke.setEnabled({{enabledLiteral}});
                }
            })();
            """;
    }
}
