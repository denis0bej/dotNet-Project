// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Funcția pentru Like/Voturi cu animații avansate
async function toggleLike(bookmarkId) {
    try {
        // Căutăm token-ul de securitate
        const tokenElement = document.getElementsByName('__RequestVerificationToken')[0];

        // Dacă nu avem token (userul nu e logat), îl trimitem la login
        if (!tokenElement) {
            window.location.href = "/Identity/Account/Login";
            return;
        }

        const token = tokenElement.value;

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

            // 1. Actualizăm numărul de like-uri
            if (countSpan) countSpan.innerText = data.newCount;

            // 2. Gestionăm animațiile
            if (heartIcon) {
                // Resetăm orice animație anterioară
                heartIcon.classList.remove('animate-like', 'animate-break');

                // Curățăm stilurile inline (transform/opacity) rămase de la animații anterioare
                heartIcon.style.transform = '';
                heartIcon.style.opacity = '';

                if (data.isLiked) {
                    // === CAZUL: LIKE (Inimă Plină Elastică) ===

                    // Schimbăm iconița în inimă plină
                    heartIcon.classList.remove('bi-heart', 'bi-heartbreak-fill', 'text-dark');
                    heartIcon.classList.add('bi-heart-fill', 'text-danger', 'animate-like');

                    // Scoatem clasa de animație după ce se termină (600ms) ca să rămână statică
                    setTimeout(() => {
                        heartIcon.classList.remove('animate-like');
                    }, 600);

                } else {
                    // === CAZUL: UNLIKE (Inimă Frântă) ===

                    // Pas 1: Schimbăm INSTANT în inimă frântă plină
                    heartIcon.classList.remove('bi-heart-fill', 'bi-heart');
                    heartIcon.classList.add('bi-heartbreak-fill', 'text-danger', 'animate-break');

                    // Pas 2: Așteptăm să se termine animația de rupere (750ms)
                    setTimeout(() => {
                        // Pas 3: Punem înapoi inima goală (neutră)
                        heartIcon.classList.remove('bi-heartbreak-fill', 'text-danger', 'animate-break');
                        heartIcon.classList.add('bi-heart', 'text-dark');
                    }, 700);
                }
            }
        }
    } catch (error) {
        console.error("Eroare la procesarea like-ului:", error);
    }
}

function toggleMobileSearch() {
    const overlay = document.getElementById('mobile-search-overlay');
    const input = document.getElementById('mobile-search-input');

    if (overlay.classList.contains('d-none')) {
        // Deschidem
        overlay.classList.remove('d-none');
        overlay.classList.add('d-flex');
        // Punem focus automat pe scris
        if (input) setTimeout(() => input.focus(), 100);
    } else {
        // Închidem
        overlay.classList.remove('d-flex');
        overlay.classList.add('d-none');
    }
}