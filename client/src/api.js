const apiBase = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5055';
const BASE_URL = apiBase.endsWith('/api') ? apiBase : `${apiBase}/api`;

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
    } catch {
      throw new Error(`API Error: ${response.status} ${response.statusText}`);
    }
    const error = new Error('Validation Error');
    error.data = errorData;
    throw error;
  }

  if (response.status === 204) {
    return null;
  }

  return response.json();
}
