// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Funcția pentru Like/Voturi
async function toggleLike(bookmarkId) {
    try {
        // Căutăm token-ul de securitate
        const tokenElement = document.getElementsByName('__RequestVerificationToken')[0];
        const token = tokenElement ? tokenElement.value : '';

        // Selectăm elementele DOM
        const heartIcon = document.getElementById(`heart-icon-${bookmarkId}`);
        const countSpan = document.getElementById(`like-count-${bookmarkId}`);

        const response = await fetch('/Bookmarks/ToggleLike', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': token
            },
            body: JSON.stringify(bookmarkId)
        });

        if (response.ok) {
            const data = await response.json();

            // Actualizăm numărul de like-uri
            if (countSpan) countSpan.innerText = data.newCount;

            // Actualizăm iconița și animațiile
            if (heartIcon) {
                // Curățăm clasele vechi de animație pentru a putea re-declanșa
                heartIcon.classList.remove('animate-like', 'animate-break');

                // Resetăm stilurile manuale (necesar după animația de break care folosește 'forwards')
                heartIcon.style.transform = '';
                heartIcon.style.opacity = '';

                if (data.isLiked) {
                    // === CAZUL: LIKE (Bătaie de inimă) ===
                    heartIcon.classList.remove('bi-heart', 'bi-heartbreak-fill');
                    heartIcon.classList.add('bi-heart-fill', 'text-danger', 'animate-like');

                    // Scoatem clasa de animație după ce se termină
                    setTimeout(() => heartIcon.classList.remove('animate-like'), 600);

                } else {
                    // === CAZUL: UNLIKE (Inimă Frântă) ===

                    // 1. Schimbăm instant în inimă frântă
                    heartIcon.classList.remove('bi-heart-fill', 'bi-heart');
                    heartIcon.classList.add('bi-heartbreak-fill', 'text-danger', 'animate-break');

                    // 2. Așteptăm să se termine animația (700ms)
                    setTimeout(() => {
                        // 3. Punem înapoi inima goală
                        heartIcon.classList.remove('bi-heartbreak-fill', 'text-danger', 'animate-break');
                        heartIcon.classList.add('bi-heart');

                        // Reset forțat la stiluri
                        heartIcon.style.transform = '';
                        heartIcon.style.opacity = '';
                    }, 700);
                }
            }
        }
    } catch (error) {
        console.error("Eroare la procesarea like-ului:", error);
    }
}