import React, { PropTypes } from 'react';
import ActionButton from 'console/components/presentation/ActionButton.js';
import Select from 'react-select';

const Toolbar = ({ style, items, canSave }) => (
	<div className={'toolbar' + (style ? ' ' + style : '')}>
		{Object.keys(items).map(name => {
			let item = Object.assign({}, items[name]);
			switch (item.type) {
			case 'select':
				item.options = item.options.map(option => ({
					value: option.value,
					label: option.label || option.value
				}));
				return <Select key="name" {...item}/>;
			case 'button':
			default:
				if (!((item.label || item.icon) && item.action)) return null;
				item.disabled = item.saveButton && !canSave;
				delete item.saveButton;
				return <ActionButton
					key={name}
					{...item}/>;
			}
		}).filter(item => !!item)}
	</div>
);

Toolbar.propTypes = {
	style: PropTypes.string,
	items: PropTypes.object.isRequired,
	canSave: PropTypes.bool
};

export default Toolbar;
