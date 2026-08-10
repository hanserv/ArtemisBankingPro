function isClientRoleSelected(selectElement) {
    const selectedOption = selectElement.options[selectElement.selectedIndex];

    return selectedOption?.textContent.trim() === 'Client';
}

function updateInitialAmountVisibility(roleSelect, initialAmountContainer) {
    const isClient = isClientRoleSelected(roleSelect);

    initialAmountContainer.classList.toggle('d-none', !isClient);
}

function setupInitialAmountVisibility(selectId, containerId) {
    const roleSelect = document.getElementById(selectId);
    const initialAmountContainer = document.getElementById(containerId);

    if (!roleSelect || !initialAmountContainer) {
        return;
    }

    roleSelect.addEventListener('change', () => {
        updateInitialAmountVisibility(roleSelect, initialAmountContainer);
    });

    updateInitialAmountVisibility(roleSelect, initialAmountContainer);
}