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
  // Chrome may expose navigator.xr while reporting no immersive device.
  // The support check above preserves real headsets; this explicit QA path
  // must replace that unsupported stub rather than silently doing nothing.
  device.installRuntime({ forceInstall: true });
  developerControls = new DevUI(device);
  emulatedDevice = device;
  if(new URL(location.href).searchParams.get('qa')==='1')window.falsePositiveEmulator=device;
  document.documentElement.dataset.xrEmulated = 'true';
  return device;
}
