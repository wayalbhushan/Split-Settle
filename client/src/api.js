const BASE_URL = import.meta.env.VITE_API_URL || 'http://127.0.0.1:8000/api';

export async function fetchApi(path, options = {}) {
  const url = `${BASE_URL}${path}`;
  const response = await fetch(url, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...options.headers,
    },
  });

  if (!response.ok) {
    let errorData;
    try {
      errorData = await response.json();
    } catch (e) {
      throw new Error(`API Error: ${response.status} ${response.statusText}`);
    }
    const error = new Error('Validation Error');
    error.data = errorData;
    throw error;
  }

  return response.json();
}
