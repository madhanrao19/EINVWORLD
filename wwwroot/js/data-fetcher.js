// wwwroot/js/data-fetcher.js

/**
 * Fetches JSON data from a given URL.
 * @param {string} url - The URL to fetch data from.
 * @returns {Promise<Object>} - The JSON data.
 */
async function fetchJsonData(url) {
    try {
        const response = await fetch(url);
        if (!response.ok) {
            throw new Error('Network response was not ok');
        }
        return await response.json();
    } catch (error) {
        console.error('Failed to fetch JSON data:', error);
    }
}
