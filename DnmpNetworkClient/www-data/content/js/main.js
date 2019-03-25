window.clientStorage = {};

$.fn.extend({
	scrollToMe: function() {
	    var x = jQuery(this).offset().top - 100;
	    jQuery('html,body').animate({scrollTop: x}, 100);
}
});

window.language = {
	'client-state-0': 'Не подключено',
	'client-state-2': 'Подключение',	
	'client-state-1': 'Подключено',
	'client-state-3': 'Соединение',
	'client-state-4': 'Отключение',
	'config-group-ClientConfig': 'Настройки Dnmp',
	'config-group-GeneralConfig': 'Общие настройки',
	'config-group-NetworksSaveConfig': 'Настройки сохранения сетей',
	'config-group-StunConfig': 'Настройки STUN',
	'config-group-TapConfig': 'Настройки TAP',
	'config-group-VisualizationConfig': 'Настройки визуализации',
	'config-group-WebServerConfig': 'Настройки веб-части',
	'config-property-ClientConfig-ClientTimeout': 'Таймаут отключения по Heartbeat-у',
	'config-property-ClientConfig-ConnectionTimeout': 'Таймаут подключения',
	'config-property-ClientConfig-HeartbeatDelay': 'Частота отправки Heartbeat-ов',
	'config-property-ClientConfig-MaxPingAnswerTime': 'Таймаут приёма пинга от клиента',
	'config-property-ClientConfig-MaxReliableRetries': 'Максимальное число попыток отправить сообщение с контролем доставки',
	'config-property-ClientConfig-PingSize': 'Размер пинга',
	'config-property-ClientConfig-PingUpdateTimerDelay': 'Частота отправки PingUpdate-а',
	'config-property-ClientConfig-PingUpdateTimerStartDelay': 'Задержка отправки PingUpdate-а',
	'config-property-ClientConfig-RebalancingTimeout': 'Частота ребаланса графа',
	'config-property-ClientConfig-ReconnectionTimeout': 'Таймаут переподключения',
	'config-property-ClientConfig-TokenSize': 'Размер токена подключения',
	'config-property-ClientConfig-ForcePingUpdateDelay': 'Частота отправки первого PingUpdate-а',
	'config-property-GeneralConfig-ReceiveBufferSize': 'Размер буффера приём',
	'config-property-GeneralConfig-SendBufferSize': 'Размер буфера отправки',
	'config-property-GeneralConfig-DefaultRsaKeySize': 'Стандартный размер RSA ключа',
	'config-property-NetworksSaveConfig-SaveFile': 'Имя файла сетей',
	'config-property-NetworksSaveConfig-SavedEndPointTtl': 'Время до удаления клиента',
	'config-property-NetworksSaveConfig-SavedEndPointsCleanUpInterval': 'Частота очистки клиентов',
	'config-property-StunConfig-Host': 'Хост STUN',
	'config-property-StunConfig-Port': 'Порт STUN',
	'config-property-StunConfig-PortMappingTimeout': 'Таймаут UPnP/PMP',
	'config-property-StunConfig-PunchPort': 'Стандартный исходящий порт',
	'config-property-TapConfig-SelfName': 'Текущее доменное имя',
	'config-property-TapConfig-IpPrefix': 'Префикс IP адреса',
	'config-property-TapConfig-MacPrefix': 'Префикс MAC адреса',
	'config-property-TapConfig-DnsFormat': 'Формат доменов внутри сети',
	'config-property-VisualizationConfig-ServerIp': 'IP сервера визуализации',
	'config-property-VisualizationConfig-ServerPort': 'Порт сервера визуализации',
	'config-property-WebServerConfig-HttpServerIp': 'Внешний IP веб-части',
	'config-property-WebServerConfig-HttpServerPort': 'Внешний порт HTTP сервера',
	'config-property-WebServerConfig-WebSocketServerPort': 'Внешний порт WS сервера',
	'config-property-WebServerConfig-WebSocketTimeout': 'Таймаут подключения к WS'
};

function notify(type, text) {
	$.notify({
		message: text,
	},{
		// settings
		element: 'body',
		position: null,
		type: type,
		allow_dismiss: true,
		newest_on_top: false,
		placement: {
			from: "bottom",
			align: "right"
		},
		template: '<div data-notify="container" class="col-2 alert alert-{0}" role="alert">' +
		'<button type="button" aria-hidden="true" class="close" data-notify="dismiss">×</button>' +
		'<span data-notify="message">{2}</span>' +
		'</div>' 
	});
}

function updateStateGui(state, notification = true) {
	window.clientStorage.currentState = state;
	switch (state) {
		case 0:
			$('#clients-table-content').hide();
			$('#client-state-text').attr("class", "text-danger");
			$('#button-network-generate-connect').show();
			$('#button-network-generate-start').show();
			$('#button-network-generate-disconnect').hide();
			if (notification)
				notify('danger', 'Отключён от сети (подключение не удалось)');
			break;
		case 1:
			$('#clients-table-content').find("tr:gt(0)").remove();
			$('#clients-table-content').show();
			$('#client-state-text').attr("class", "text-success");
			$('#button-network-generate-connect').hide();
			$('#button-network-generate-start').hide();
			$('#button-network-generate-disconnect').show();
			if (notification)
				notify('success', 'Подключено');
			break;
		case 2:
			if (notification)
				notify('warning', 'Подключение...');
		case 3:
		case 4:
			$('#clients-table-content').hide();
			$('#client-state-text').attr("class", "text-warning");
			$('#button-network-generate-connect').hide();
			$('#button-network-generate-start').hide();
			$('#button-network-generate-disconnect').show();
			break;
	}
	$('#client-state-text').text(window.language['client-state-' + state]);
}

function updateClientRow(id, data) {
	if (data == null) {
		$('#connected-client-' + id).remove();
		$('#clients-count').text($('#clients-table-content tr').length - 1);
		return;
	}
	if (!$('#connected-client-' + id).length) {
		$('#clients-table-content').find('tbody').append(
			$('<tr>').append(
				$('<td>').attr('id', 'connected-client-' + id + '-id')
				.text(data.id)
			).append(
				$('<td>').attr('id', 'connected-client-' + id + '-parent-id').attr('class', 'dev-mode')
				.text(data.parentId == 65535 ? "-" : data.parentId)
			).append(
				$('<td>').attr('id', 'connected-client-' + id + '-public-ipport').attr('class', 'dev-mode')
				.text(data.publicIpPort)
			).append(
				$('<td>').attr('id', 'connected-client-' + id + '-internal-ip').attr('class', 'dev-mode')
				.text(data.internalIp)
			).append(
				$('<td>').attr('id', 'connected-client-' + id + '-internal-domain')
				.text(data.internalDomain)
			).append(
				$('<td>').attr('id', 'connected-client-' + id + '-direct-connection')
				.html(data.id == window.clientStorage.selfId ? "-" : data.flags & 4 ? '<i class="fas fa-check text-success"></i>' : '<i class="fas fa-times text-danger"></i>')
			).append(
				$('<td>').attr('id', 'connected-client-' + id + '-ping')
				.text(data.ping == 65535 ? "-" : data.ping + ' мс')
			).append(
				$('<td>').attr('id', 'connected-client-' + id + '-bytes-received')
				.text(data.id == window.clientStorage.selfId ? "-" : data.bytesReceived)
			).append(
				$('<td>').attr('id', 'connected-client-' + id + '-bytes-sent')
				.text(data.id == window.clientStorage.selfId ? "-" : data.bytesSent)
			).append(
				$('<td>').attr('id', 'connected-client-' + id + '-data-bytes-received').attr('class', 'dev-mode')
				.text(data.id == window.clientStorage.selfId ? "-" : data.dataBytesReceived)
			).append(
				$('<td>').attr('id', 'connected-client-' + id + '-data-bytes-sent').attr('class', 'dev-mode')
				.text(data.id == window.clientStorage.selfId ? "-" : data.dataBytesSent)
			).attr('class', id == window.clientStorage.selfId ? 'self-client-row' : '')
			.attr('id', 'connected-client-' + id)
		);
		$('#clients-count').text($('#clients-table-content tr').length - 1);
		updateDevModeElements();
	} else {
		$('#connected-client-' + id + '-id').text(data.id);
		$('#connected-client-' + id + '-parent-id').text(data.parentId == 65535 ? "-" : data.parentId);
		$('#connected-client-' + id + '-public-ipport').text(data.publicIpPort);
		$('#connected-client-' + id + '-internal-ip').text(data.internalIp);
		$('#connected-client-' + id + '-internal-domain').text(data.internalDomain);
		$('#connected-client-' + id + '-direct-connection').html(data.id == window.clientStorage.selfId ? "-" : data.flags & 4 ? '<i class="fas fa-check text-success"></i>' : '<i class="fas fa-times text-danger"></i>');
		$('#connected-client-' + id + '-ping').text(data.ping == 65535 ? "-" : data.ping + ' мс');
		$('#connected-client-' + id + '-bytes-received').text(data.id == window.clientStorage.selfId ? "-" : data.bytesReceived);
		$('#connected-client-' + id + '-bytes-sent').text(data.id == window.clientStorage.selfId ? "-" : data.bytesSent);
		$('#connected-client-' + id + '-data-bytes-received').text(data.id == window.clientStorage.selfId ? "-" : data.dataBytesReceived);
		$('#connected-client-' + id + '-data-bytes-sent').text(data.id == window.clientStorage.selfId ? "-" : data.dataBytesSent);
	}
}

function updateDevModeElements() {
	if ($('#dev-mode')[0].checked)
		$('.dev-mode').show();
	else
		$('.dev-mode').hide();
}

function updateNetworkRow(data) {
	var id = data.id;
	if (!$('#network-table-row-' + id).length) {
		$('#networks-table').find('tbody').append(
			$('<tr>').append(
				$('<td>').attr('id', 'network-' + id + '-id')
				.text(data.id)
			).append(
				$('<td>').attr('id', 'network-' + id + '-name')
				.text(data.name)
			).append(
				$('<td>').attr('id', 'network-' + id + '-saved-clients-count')
				.text(data.savedClients)
			).attr('id', 'network-table-row-' + id).attr('data-id', id).attr('class', 'network-table-row')
		);
		$('#network-table-row-' + id).click(function() {
			$('.network-table-row').removeClass('active');
			$(this).addClass('active');
			window.clientStorage.selectedNetworkId = this.dataset.id;
			updateNetworkControlButtons();
		});
	} else {
		$('#network-' + id + '-id').text(data.id);
		$('#network-' + id + '-name').text(data.name);
		$('#network-' + id + '-saved-clients-count').text(data.savedClients);
	}
}

function updateNetworkControlButtons() {
	var state = window.clientStorage.selectedNetworkId != null ? null : 'disabled';
	$('#button-network-generate-key-code').attr('disabled', state);
	$('#button-network-generate-invite-code').attr('disabled', state);
	$('#button-network-generate-connect').attr('disabled', state);
	$('#button-network-generate-start').attr('disabled', state);
	$('#button-network-generate-remove').attr('disabled', state);
}

function generateConfigGroup(groupName, config) {
	var output = $('<fieldset>').attr('class', 'form-group').attr('style', 'padding: 20px; border-radius: 5px; border: 2px groove threedface');
	output.append($('<legend>').attr('style', 'padding-left: 10px; padding-right: 10px; width: initial').text(window.language['config-group-' + groupName])); 
	for (var property in config) {
		if (property.endsWith('Regexp'))
			continue;
		output.append($('<div>').attr('class', 'form-group row')
			.append(
				$('<label>').attr('class', 'col-sm-3 col-form-label').attr('for', 'config-input-' + groupName + '-' + property).text(window.language['config-property-' + groupName + '-' + property] + ':')
			)
			.append(
				$('<div>').attr('class', 'col-sm-9')
				.append(
					$('<input>')
						.attr('class', 'form-control config-input')
						.attr('type', 'text')
						.attr('data-regexp', config[property + 'Regexp'])
						.attr('data-config-group', groupName)
						.attr('data-config-property', property)
						.attr('value', config[property])
						.attr('id', 'config-input-' + groupName + '-' + property)
				)
			)
		);
	}
	return output;
}

function processMessage(event) {
	var data = JSON.parse(event.data);
	switch (data.eventType) {
		case 0: // Initialization
			updateStateGui(data.eventData.networkInfo.state, false);
			window.clientStorage.selfId = data.eventData.networkInfo.selfClientId;
			if (data.eventData.networkInfo.clients != null)
				for (var i = 0; i < data.eventData.networkInfo.clients.length; i++)
					updateClientRow(data.eventData.networkInfo.clients[i].id, data.eventData.networkInfo.clients[i]);

			$('#client-settings-list').empty();
			var config = data.eventData.networkInfo.config;
			for (var property in config) {
				$('#client-settings-list').append(generateConfigGroup(property, config[property]));
			}
			$('.config-input').on('change keyup keydown', function() {
				var regex = new RegExp("^" + $(this).data('regexp') + "$");
				if (regex.test($(this).val())) {
					$(this).removeClass('is-invalid');
					$(this).addClass('is-valid');
				} else {
					$(this).addClass('is-invalid');
					$(this).removeClass('is-valid');
				}
			});
			$('.config-input').trigger('change');
			$('#start-bg-loader').fadeOut('fast');
			$('#row-main').fadeIn('fast');
			break;

		case 1: 
			var state = data.eventData.status;
			updateStateGui(state);
			switch (state) {
				case 1:
					window.clientStorage.selfId = data.eventData.selfClient.id;
					updateClientRow(data.eventData.selfClient.id, data.eventData.selfClient);
					break;
			}
			break;

		case 2: // ClientConnect
			updateClientRow(data.eventData.newClient.id, data.eventData.newClient);
			break;

		case 3: // ClientDisconnect
			updateClientRow(data.eventData.clientId, null);
			break;

		case 4: // ClientUpdate
			updateClientRow(data.eventData.client.id, data.eventData.client);
			break;

		case 5: // NetworkListUpdate
			var rows = $('.network-table-row');
			var currentIds = {};
			for (var i = 0; i < data.eventData.networks.length; i++) {
				updateNetworkRow(data.eventData.networks[i]);
				currentIds[data.eventData.networks[i].id] = true;
			}

			for (var i = 0; i < rows.length; i++)
				if (!currentIds[rows[i].dataset.id]) {
					$('#network-table-row-' + rows[i].dataset.id).remove();
					if (rows[i].dataset.id == window.clientStorage.selectedNetworkId) {
						window.clientStorage.selectedNetworkId = null;
						updateNetworkControlButtons();
					}
				}
			break;
	}
}

function reinitWebSocket() {
	$('#processing-loader').fadeOut('fast');
	$('#start-bg-loader').fadeIn('fast');
	$('#row-main').fadeOut('fast');
	window.clientStorage.webSocket = new WebSocket(window.clientStorage.webSocketAddress);
	window.clientStorage.webSocket.onclose = reinitWebSocket;
	window.clientStorage.webSocket.onmessage = processMessage;
	window.clientStorage.webSocket.onopen = function() {
		$('.network-table-row').remove();
		window.clientStorage.selectedNetworkId = null;
		updateNetworkControlButtons();
		window.clientStorage.webSocket.send(JSON.stringify({
			requestType: 0,
			requestData: null
		}));
	};
}

jQuery(document).ready(function($) {
	$('.sidebar-button').click(function() {
		$('.sidebar-button').removeClass('sidebar-button-active');
		$(this).addClass('sidebar-button-active');
		$('.content-part').hide();
		$('#content-part-' + $(this).data('content-id')).show();
	});	

	$('.content-part').hide();
	$('#content-part-' + $('.sidebar-button').first().data('content-id')).show();

	$('#network-create-modal-button').click(function() {
		$('#network-create-modal').modal('hide');
		$('#processing-loader').fadeIn('fast');
		$('#processing-status').text('Создание сети...');
		$.post('/api/createnetwork', JSON.stringify({
			requestData: {
				keySize: $('#network-create-key-size').val() == "" ? null : $('#network-create-key-size').val(),
				name: $('#network-create-name').val() == "" ? null : $('#network-create-name').val()
			}
		}), function(data) {
			$('#processing-loader').fadeOut('fast', 'swing');
			notify('success', 'Сеть успешно создана');
		});
	});

	$('#network-add-network-modal-button').click(function() {
		$('#network-add-network-modal').modal('hide');
		$('#processing-loader').fadeIn('fast');
		$('#processing-status').text('Создание сети...');
		$.post('/api/addnetwork', JSON.stringify({
			requestData: {
				key: $('#network-add-network-key-code').val(),
				name: $('#network-add-network-name').val() == "" ? null : $('#network-add-network-name').val()
			}
		}), function(data) {
			$('#processing-loader').fadeOut('fast', 'swing');
			if (data.error == null)
				notify('success', 'Сеть успешно добавлена. ID: ' + data.networkId);
			else if (data.error == 'network-already-exists')
				notify('warning', 'Сеть с таким ключ-кодом уже есть. ID: ' + data.networkId);
			else
				notify('danger', 'Неправильный формат ключ-кода');
		});
	});

	$('#network-accept-invitation-modal-button').click(function() {
		$('#network-accept-invitation-modal').modal('hide');
		$('#processing-loader').fadeIn('fast');
		$('#processing-status').text('Обработка инвайт-кода...');
		$.post('/api/processinvite', JSON.stringify({
			requestData: {
				inviteCode: $('#network-accept-invitation-invite-code').val()
			}
		}), function(data) {
			$('#processing-loader').fadeOut('fast', 'swing');
			if (data.error == null)
				notify('success', 'В сеть с ID ' + data.networkId + ' успешно добавлено ' + data.count + ' клиентов');
			else if (data.error == 'invite-code-network-not-found')
				notify('warning', 'Сеть с таким инвайт-кодом не существует. ID: ' + data.networkId);
			else
				notify('danger', 'Неправильный формат инвайт-кода');
		});
	});

	$('#button-network-generate-key-code').click(function() {
		$('#processing-loader').fadeIn('fast');
		$('#processing-status').text('Генерация ключ-кода...');
		$.post('/api/generatekey', JSON.stringify({
			requestData: {
				networkId: window.clientStorage.selectedNetworkId
			}
		}), function(data) {
			$('#processing-loader').fadeOut('fast', 'swing', function() {
								$('#key-code-output').text(data.text);
				$('#network-generate-key-code-output-modal').modal('show');
			});
			notify('success', 'Ключ-код сгенерирован успешно');
		});
	});

	$('#button-network-generate-invite-code').click(function() {
		$('#processing-loader').fadeIn('fast');
		$('#processing-status').text('Генерация инвайт-кода...');
		$.post('/api/generateinvite', JSON.stringify({
			requestData: {
				networkId: window.clientStorage.selectedNetworkId,
				maxLength: 100000000
			}
		}), function(data) {
			$('#processing-loader').fadeOut('fast', 'swing', function() {
				$('#invite-code-output').text(data.text);
				$('#network-generate-invite-code-output-modal').modal('show');
			});
			notify('success', 'Инвайт-код сгенерирован успешно');
		});
	});

	$('#button-network-generate-remove').click(function() {
		$('#processing-loader').fadeIn('fast');
		$('#processing-status').text('Удаление сети...');
		$.post('/api/removenetwork', JSON.stringify({
			requestData: {
				networkId: window.clientStorage.selectedNetworkId
			}
		}), function(data) {
			$('#processing-loader').fadeOut('fast', 'swing', function() {});
			notify('success', 'Сеть удалена успешно');
		});
	});

	$('#network-connect-default-port').change(function() {
		if (this.checked)
			$('#network-connect-port').prop('disabled', true);
		else
			$('#network-connect-port').prop('disabled', false);
	});

	$('#network-start-default-port').change(function() {
		if (this.checked)
			$('#network-start-port').prop('disabled', true);
		else
			$('#network-start-port').prop('disabled', false);
	});

	$('#network-start-use-stun').change(function() {
		if (this.checked)
			$('#network-start-ip').prop('disabled', true);
		else
			$('#network-start-ip').prop('disabled', false);
	});

	$('#network-connect-modal-button').click(function() {
		$('#network-connect-modal').modal('hide');
		$('#processing-loader').fadeIn('fast');
		$('#processing-status').text('Запуск подключения...');
		$.post('/api/connecttonetwork', JSON.stringify({
			requestData: {
				networkId: window.clientStorage.selectedNetworkId,
				publicIp: '',
				sourcePort: $('#network-connect-default-port')[0].checked ? null : $('#network-connect-port').val(),
				startAsFirst: false,
				useUpnp: $('#network-connect-use-upnp')[0].checked,
				useStun: false
			}
		}), function(data) {
			$('#processing-loader').fadeOut('fast', 'swing');
			if (data.error == null)
				return;
			else
				notify('danger', 'Ошибка при обработке подключения');
		});
	});

	$('#network-start-modal-button').click(function() {
		$('#network-start-modal').modal('hide');
		$('#processing-loader').fadeIn('fast');
		$('#processing-status').text('Запуск подключения...');
		$.post('/api/connecttonetwork', JSON.stringify({
			requestData: {
				networkId: window.clientStorage.selectedNetworkId,
				sourcePort: $('#network-start-default-port')[0].checked ? null : $('#network-start-port').val(),
				startAsFirst: true,
				useUpnp: $('#network-start-use-upnp')[0].checked,
				useStun: $('#network-start-use-stun')[0].checked,
				publicIp: $('#network-start-ip').val()
			}
		}), function(data) {
			$('#processing-loader').fadeOut('fast', 'swing');
			if (data.error == null)
				return;
			else if (data.error == 'stun-error')
				notify('danger', 'Ошибка STUN при определении внещнего IP/порта. Попробуйте вручную сделать проброс порта или обратится к администратору сети.');
			else
				notify('danger', 'Ошибка при обработке подключения');
		});
	});

	$('#button-network-generate-disconnect').click(function() {
		window.clientStorage.webSocket.send(JSON.stringify({
			requestType: 1,
			requestData: null
		}));
	});

	$('#save-client-config').click(function() {
		var configInputs = $('.config-input');
		var output = {};
		for (var i = 0; i < configInputs.length; i++) {
			var element = $(configInputs[i]);
			var regex = new RegExp("^" + element.data('regexp') + "$");
			if (!regex.test(element.val())) {
				element.scrollToMe();
				element.focus();
				return;
			} else {
				if (output[element.data('config-group')] == undefined)
					output[element.data('config-group')] = {};
				output[element.data('config-group')][element.data('config-property')] = element.val();
			}
		}
		window.clientStorage.lastConfig = output;
		$('#save-config-modal').modal('show');
	});

	$('#save-config-modal-button').click(function() {
		$('#save-config-modal').modal('hide');
		$('#processing-loader').fadeIn('fast');
		$('#processing-status').text('Сохранение настроек...');
		$.post('/api/updateconfig', JSON.stringify({
			requestData: {
				newConfigJson: JSON.stringify(window.clientStorage.lastConfig)
			}
		}));
	});

	$('#dev-mode').click(updateDevModeElements);

	updateDevModeElements();

	$.post('/api/getwebsocketport', JSON.stringify({}), function(data) {
		window.clientStorage.webSocketAddress = 'ws://' + location.hostname + ':' + data.port;
		reinitWebSocket();
	});
});