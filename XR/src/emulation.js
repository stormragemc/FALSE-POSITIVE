// Explicit desktop QA only. Native headset sessions remain the default.
// Public API: https://meta-quest.github.io/immersive-web-emulation-runtime/getting-started.html
let emulatedDevice = null;
let developerControls = null;

export async function setupEmulation() {
  if (new URL(window.location.href).searchParams.get('emulate') !== '1') return null;
  if (emulatedDevice) return emulatedDevice;

  // An actual headset (or an existing browser emulator) owns its XR runtime.
  try {
    if (await navigator.xr?.isSessionSupported('immersive-vr')) return null;
  } catch {
    // Unsupported or unavailable native XR is exactly what this QA path covers.
  }

  const [{ XRDevice, metaQuest3 }, { DevUI }] = await Promise.all([
    import('iwer'),
    import('@iwer/devui'),
  ]);
  const device = new XRDevice(metaQuest3);
  device.position.set(0, 1.2, 0);
  device.primaryInputMode = 'hand';
  device.installRuntime();
  developerControls = new DevUI(device);
  emulatedDevice = device;
  document.documentElement.dataset.xrEmulated = 'true';
  return device;
}
