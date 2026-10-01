import * as DocumentPicker from 'expo-document-picker';

export const MAX_CV_BYTES = 5 * 1024 * 1024;


export type PickedCv = { form: FormData; name: string };

export class CvPickError extends Error {}

/** Opens the system file picker and returns the multipart body, or null if the user cancelled. */
export async function pickCv(): Promise<PickedCv | null> {
  const result = await DocumentPicker.getDocumentAsync({ type: ['application/pdf', 'application/vnd.openxmlformats-officedocument.wordprocessingml.document'], copyToCacheDirectory: true, multiple: false });
  if (result.canceled || result.assets.length === 0) return null;

  const asset = result.assets[0];
  if (asset.size !== undefined && asset.size > MAX_CV_BYTES) throw new CvPickError('Tu archivo pesa más de 5 MB. Sube una versión más liviana.');
  if (!/\.(pdf|docx)$/i.test(asset.name)) throw new CvPickError('Solo aceptamos CV en PDF o Word (.docx).');

  // SDK 57's fetch only accepts real Blob/File parts (the old `{ uri, name, type }` object is rejected as an
  // "Unsupported FormDataPart"). `asset.file` is the picker's File; otherwise read the cached copy as a Blob.
  const part: Blob = asset.file ?? (await (await fetch(asset.uri)).blob());
  const form = new FormData();
  form.append('file', part, asset.name);
  return { form, name: asset.name };
}
