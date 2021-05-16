const FanartConfig = {
    pluginUniqueId: '170a157f-ac6c-437a-abdd-ca9c25cebd39'
};

export default function (view) {
    view.addEventListener('viewshow', function () {
        Dashboard.showLoadingMsg();
        const page = this;
        ApiClient.getPluginConfiguration(FanartConfig.pluginUniqueId).then(function (config) {
            page.querySelector('#apikey').value = config.PersonalApiKey || '';
            Dashboard.hideLoadingMsg();
        });
    });

    view.querySelector('#FanartConfigForm').addEventListener('submit', function (e) {
        Dashboard.showLoadingMsg();
        const form = this;
        ApiClient.getPluginConfiguration(FanartConfig.pluginUniqueId).then(function (config) {
            config.PersonalApiKey = form.querySelector('#apikey').value;
            ApiClient.updatePluginConfiguration(FanartConfig.pluginUniqueId, config).then(function (result) {
                Dashboard.processPluginConfigurationUpdateResult(result);
            });
        });
        e.preventDefault();
        return false;
    });
}
